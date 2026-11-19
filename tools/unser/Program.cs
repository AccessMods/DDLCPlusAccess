using System.Text;
using System.Text.Json;
using Mono.Cecil;

// usage: unser <managedDir> <assembly> <className> <in.bin> <out.json> [enumSizeMode]
var managed = args[0];
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managed);
var asm = AssemblyDefinition.ReadAssembly(Path.Combine(managed, args[1]), new ReaderParameters { AssemblyResolver = resolver });
var root = asm.MainModule.GetTypes().First(t => t.Name == args[2]);
var data = File.ReadAllBytes(args[3]);
var r = new Reader(data);
using var fs = File.Create(args[4]);
using var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
w.WriteStartObject();
w.WritePropertyName("m_GameObject"); r.PPtr(w);
w.WriteNumber("m_Enabled", r.U8()); r.Align();
w.WritePropertyName("m_Script"); r.PPtr(w);
w.WriteString("m_Name", r.Str());
var ser = new Ser(r, w);
ser.Fields(root, 0);
w.WriteEndObject();
w.Flush();
Console.WriteLine($"consumed {r.Pos} of {data.Length}");

class Reader {
  public byte[] D; public int Pos;
  public Reader(byte[] d) { D = d; }
  public byte U8() => D[Pos++];
  public short I16() { var v = BitConverter.ToInt16(D, Pos); Pos += 2; return v; }
  public int I32() { var v = BitConverter.ToInt32(D, Pos); Pos += 4; return v; }
  public long I64() { var v = BitConverter.ToInt64(D, Pos); Pos += 8; return v; }
  public float F32() { var v = BitConverter.ToSingle(D, Pos); Pos += 4; return v; }
  public double F64() { var v = BitConverter.ToDouble(D, Pos); Pos += 8; return v; }
  public void Align() { Pos = (Pos + 3) & ~3; }
  public string Str() { int n = I32(); if (n < 0 || n > D.Length - Pos) throw new Exception($"bad strlen {n} at {Pos}"); var s = Encoding.UTF8.GetString(D, Pos, n); Pos += n; Align(); return s; }
  public void PPtr(Utf8JsonWriter w) { int f = I32(); long p = I64(); w.WriteStringValue($"pptr({f},{p})"); }
}

class Ser {
  Reader r; Utf8JsonWriter w;
  public Ser(Reader r, Utf8JsonWriter w) { this.r = r; this.w = w; }

  static bool HasAttr(ICustomAttributeProvider p, string name) => p.HasCustomAttributes && p.CustomAttributes.Any(a => a.AttributeType.Name == name);

  public static IEnumerable<FieldDefinition> SerFields(TypeDefinition t) {
    var chain = new List<TypeDefinition>();
    for (var c = t; c != null; ) {
      chain.Insert(0, c);
      var bt = c.BaseType;
      if (bt == null) break;
      var ns = bt.Namespace;
      if (ns.StartsWith("System") || ns.StartsWith("UnityEngine")) break;
      c = bt.Resolve();
    }
    foreach (var c in chain)
      foreach (var f in c.Fields) {
        if (f.IsStatic || f.IsInitOnly || f.IsLiteral || f.IsNotSerialized) continue;
        if (!(f.IsPublic || HasAttr(f, "SerializeField"))) continue;
        if (!IsSerializableType(f.FieldType)) continue;
        yield return f;
      }
  }

  static bool IsUnityObject(TypeReference t) {
    var d = t.Resolve();
    while (d != null) {
      if (d.FullName == "UnityEngine.Object") return true;
      d = d.BaseType?.Resolve();
    }
    return false;
  }

  static readonly HashSet<string> Builtin = new() { "UnityEngine.Vector2","UnityEngine.Vector3","UnityEngine.Vector4","UnityEngine.Quaternion","UnityEngine.Color","UnityEngine.Color32","UnityEngine.Rect","UnityEngine.AnimationCurve","UnityEngine.Vector2Int","UnityEngine.Vector3Int","UnityEngine.LayerMask","UnityEngine.Bounds","UnityEngine.Gradient","UnityEngine.Matrix4x4" };

  static bool IsSerializableType(TypeReference t) {
    if (t.IsArray) { var e = ((ArrayType)t).ElementType; return !e.IsArray && !IsList(e) && IsSerializableType(e); }
    if (IsList(t)) { var e = ((GenericInstanceType)t).GenericArguments[0]; return !e.IsArray && !IsList(e) && IsSerializableType(e); }
    if (t.IsGenericInstance || t.IsGenericParameter) return false;
    if (t.IsPrimitive || t.FullName == "System.String") return t.FullName != "System.IntPtr" && t.FullName != "System.UIntPtr";
    if (Builtin.Contains(t.FullName)) return true;
    var d = t.Resolve();
    if (d == null) return false;
    if (d.IsEnum) return true;
    if (IsUnityObject(t)) return true;
    if (d.Namespace.StartsWith("System")) return false;
    if (d.IsInterface || d.IsAbstract && !d.IsSealed) return false;
    if (d.IsSerializable) return true;
    return false;
  }

  static bool IsList(TypeReference t) => t.IsGenericInstance && t.Resolve()?.FullName == "System.Collections.Generic.List`1";

  public void Fields(TypeDefinition t, int depth) {
    foreach (var f in SerFields(t)) {
      w.WritePropertyName(f.Name);
      Value(f.FieldType, depth);
    }
  }

  void Value(TypeReference t, int depth) {
    if (t.IsArray || IsList(t)) {
      var e = t.IsArray ? ((ArrayType)t).ElementType : ((GenericInstanceType)t).GenericArguments[0];
      int n = r.I32();
      if (n < 0 || n > 10_000_000) throw new Exception($"bad array len {n} at {r.Pos} for {t.FullName}");
      w.WriteStartArray();
      for (int i = 0; i < n; i++) Value(e, depth + 1);
      w.WriteEndArray();
      r.Align();
      return;
    }
    switch (t.FullName) {
      case "System.Boolean": w.WriteBooleanValue(r.U8() != 0); r.Align(); return;
      case "System.Byte": w.WriteNumberValue(r.U8()); r.Align(); return;
      case "System.SByte": w.WriteNumberValue((sbyte)r.U8()); r.Align(); return;
      case "System.Char": w.WriteNumberValue(r.I16()); r.Align(); return;
      case "System.Int16": case "System.UInt16": w.WriteNumberValue(r.I16()); r.Align(); return;
      case "System.Int32": w.WriteNumberValue(r.I32()); return;
      case "System.UInt32": w.WriteNumberValue((uint)r.I32()); return;
      case "System.Int64": case "System.UInt64": w.WriteNumberValue(r.I64()); return;
      case "System.Single": w.WriteNumberValue(r.F32()); return;
      case "System.Double": w.WriteNumberValue(r.F64()); return;
      case "System.String": w.WriteStringValue(r.Str()); return;
      case "UnityEngine.Vector2": w.WriteStringValue($"v2({r.F32()},{r.F32()})"); return;
      case "UnityEngine.Vector3": w.WriteStringValue($"v3({r.F32()},{r.F32()},{r.F32()})"); return;
      case "UnityEngine.Vector4": case "UnityEngine.Quaternion": case "UnityEngine.Color": case "UnityEngine.Rect": w.WriteStringValue($"v4({r.F32()},{r.F32()},{r.F32()},{r.F32()})"); return;
      case "UnityEngine.Color32": case "UnityEngine.LayerMask": w.WriteNumberValue(r.I32()); return;
      case "UnityEngine.Vector2Int": w.WriteStringValue($"v2i({r.I32()},{r.I32()})"); return;
    }
    if (Builtin.Contains(t.FullName)) throw new Exception("unsupported builtin " + t.FullName);
    var d = t.Resolve();
    if (d.IsEnum) { w.WriteNumberValue(r.I32()); return; }
    if (IsUnityObject(t)) { r.PPtr(w); return; }
    if (depth > 12) throw new Exception("too deep " + t.FullName);
    w.WriteStartObject();
    Fields(d, depth + 1);
    w.WriteEndObject();
  }
}
