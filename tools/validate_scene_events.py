import sys,re; sys.path.insert(0,sys.argv[1]); from blk import load
REPO=sys.argv[2]; blocks={b[0]:b for b in load(sys.argv[3])}
def kv(path):
    d={}
    for l in open(path,encoding="utf-8"):
        l=l.strip()
        if not l or l.startswith("#") or "=" not in l: continue
        k,v=l.split("=",1); d[k.strip()]=v.strip()
    return d
en=kv(REPO+"/Localization/en-us.txt")
bad=0
for l in open(REPO+"/SceneDescriptions/SceneEvents.txt",encoding="utf-8"):
    l=l.strip()
    if not l or l.startswith("#"): continue
    loc,snip,keys=[p.strip() for p in l.split("|")]
    blk,idx=loc.rsplit(":",1); idx=int(idx)
    b=blocks.get(blk)
    line=dict(b[2]).get(idx) if b else None
    if line is None or snip not in line:
        print("MISMATCH",loc,"expected",repr(snip),"got",repr(line)); bad+=1
    for k in keys.split(","):
        k=k.strip().split(":")[0]
        if k not in en: print("MISSING KEY",k); bad+=1
for l in open(REPO+"/SceneDescriptions/SceneRules.txt",encoding="utf-8"):
    l=l.strip()
    if not l or l.startswith("#"): continue
    k=l.split("=",1)[1].split("|")[0].strip()
    if k not in en: print("MISSING RULE KEY",k); bad+=1
print("problems:",bad)

# Every translation must have the same keys and placeholders as English.
import glob, os
for path in sorted(glob.glob(REPO + "/Localization/*.txt")):
    lang = os.path.basename(path)[:-4]
    if lang == "en-us":
        continue
    tr = kv(path)
    missing = sorted(set(en) - set(tr))
    extra = sorted(set(tr) - set(en))
    ph = [k for k in en if k in tr and sorted(re.findall(r"\{\d\}", en[k])) != sorted(re.findall(r"\{\d\}", tr[k]))]
    status = "ok" if not (missing or extra or ph) else "PROBLEMS"
    print(f"{lang}: {len(tr)} keys {status}" + (f" missing={missing}" if missing else "") + (f" extra={extra}" if extra else "") + (f" placeholders={ph}" if ph else ""))
