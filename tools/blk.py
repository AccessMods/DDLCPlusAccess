import re
def load(path):
    blocks=[];cur=None
    for line in open(path,encoding="utf-8"):
        if line.startswith("==="):
            m=re.match(r"=== (.+?) \(label=(.*?) main=(\w+)\)",line); cur=[m.group(1),m.group(3)=="True",[]]; blocks.append(cur)
        elif cur and line.strip():
            idx,_,rest=line.strip().partition(" ")
            cur[2].append((int(idx),rest))
    return blocks
