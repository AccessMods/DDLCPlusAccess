import json, sys, os
X = sys.argv[1]
B=json.load(open(os.path.join(X,"blocks.json"),encoding="utf-8"))
L=json.load(open(os.path.join(X,"lines_en-us.json"),encoding="utf-8"))
A=json.load(open(os.path.join(X,"audio.json"),encoding="utf-8"))
audio={k:v["path"] for k,v in zip(A["audioKeys"],A["audioValues"])}
text={}
for e in L["linesdict"]:
    for k,v in zip(e["keys"],e["values"]): text[k]=v
TYPES=["Dialog","Show","Hide","Scene","With","Play","Stop","Queue","Function","OneLinePython","InlinePython","LabelEntryPoint","Goto","Return","GotoLine","GotoLineUnless","GotoLineTimeout","ForkGotoLine","Immediate","Ease","Pause","LoadImage","Size","Time","SetRandRange","Text","Expression","WaitForScreen","MenuInput","Window","NOP","Unlock","WindowAuto","ClrFlag"]
LIST={"Dialog":"dialogcontents","Show":"showContents","Hide":"hideContents","Scene":"sceneContents","With":"withContents","Play":"playAudioContents","Stop":"stopAudioContents","Queue":"queueAudioContents","Function":"functionContents","OneLinePython":"onelinePythonContents","InlinePython":"inlinePythonContents","LabelEntryPoint":"entryPoints","Goto":"gotoContents","GotoLine":"gotoLineContents","GotoLineUnless":"gotoLineUnlessContents","GotoLineTimeout":"gotoLineTimeoutContents","ForkGotoLine":"forkGotoLineContents","Immediate":"immediateTransforms","Ease":"easedTransforms","Pause":"pauses","LoadImage":"images","Size":"sizes","Time":"times","SetRandRange":"setRandomRanges","Text":"texts","Expression":"expressions","WaitForScreen":"waitForScreen","MenuInput":"menuInputContents","Window":"windowContents","Unlock":"unlocks","WindowAuto":"windowAuto","ClrFlag":"clrFlags"}
def cs(e): return e.get("constantStrings",[]) if e else []
def render(t,v):
    if t=="Dialog":
        return f'SAY {v["label"]}{"/"+v["variant"] if v["variant"] else ""} [{v["textID"]}] {json.dumps(text.get(v["textID"],"<??>"),ensure_ascii=False)}' + (" (DEV)" if v["developerCommentary"] else "")
    if t=="Show": return v["ShowData"]
    if t=="Hide": return v["HideData"]
    if t=="Scene": return v["SceneData"]
    if t=="With": return "with "+v["WithData"]
    if t=="Play": p=v["play"]; return f'play ch{p["Channel"]} {audio.get(p["Asset"],p["Asset"])}'
    if t=="Queue": p=v["queue"]; return f'queue {p["Channel"]} {audio.get(p["Asset"],p["Asset"])}'
    if t=="Stop": return f'stop ch{v["stop"]["Channel"]}'
    if t=="Function": return v["m_Function"]["FunctionData"]
    if t=="OneLinePython": return "$ "+" | ".join(map(str,cs(v["m_OneLinePython"]["compiledExpression"])))
    if t=="InlinePython": return "PY "+v["m_InlinePython"]["PythonData"]
    if t=="LabelEntryPoint": return "label "+v["entryPoint"]["label"]
    if t=="Goto": return ("call " if v["isCall"] else "jump ")+(v["targetLabel"] or "expr:"+"|".join(cs(v["targetExpression"])))
    if t=="GotoLine": return f'goto {v["targetLine"]}'
    if t=="GotoLineUnless": return f'unless ({v["conditionText"]}) goto {v["targetLine"]}'
    if t=="GotoLineTimeout": return f'timeout goto {json.dumps(v)[:120]}'
    if t=="ForkGotoLine": return f'fork {v["targetLine"]}'
    if t=="Immediate": return "xf "+v["TransformCommand"]
    if t=="Ease": return f'ease {v["Duration"]}'
    if t=="Pause": return "pause "+v["PauseData"]
    if t=="LoadImage": return f'image {v["key"]} = {v["fullImageDetails"]}'
    if t=="Size": return v["SizeData"]
    if t=="Time": return "time "+v["TimeData"]
    if t=="SetRandRange": return f'randrange {v["Range"]}'
    if t=="Text": return "Text "+" | ".join(" ".join(cs(c["expression"])) for c in v["CallParameters"])
    if t=="Expression": return "expr "+" | ".join(map(str,cs(v["Expr"])))
    if t=="WaitForScreen": return "waitscreen "+v["screen"]
    if t=="MenuInput": return "MENU " + " || ".join(f'{json.dumps(text.get(e["textID"],"<??>"),ensure_ascii=False)}->{e["gotoLineTarget"]}' for e in v["entries"])
    if t=="Window": return v["WindowData"]
    if t=="Unlock": return v["UnlockData"]
    if t=="WindowAuto": return f'windowauto {v["show"]} {v["command"]}'
    if t=="ClrFlag": return "clrflag "+v["flag"]
    return json.dumps(v)[:200]
out=open(os.path.join(X,"script.txt"),"w",encoding="utf-8")
for bk,bc in zip(B["blockkeys"],B["blockcontents"]):
    bk=bk.replace("\n","\\n")
    out.write(f"\n=== {bk} (label={bc['label']} main={bc['isMainLabel']})\n")
    for i,c in enumerate(bc["contents"]):
        t=TYPES[c["type"]]
        if t in ("Return","NOP"): out.write(f"  {i:4} {t.lower()}\n"); continue
        v=bc[LIST[t]][c["index"]]
        out.write(f"  {i:4} " + render(t,v).replace("\r","").replace("\n","\\n") + "\n")
