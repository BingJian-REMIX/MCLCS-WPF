#!/bin/bash
# MCLCS.App Roslyn 交叉编译 (Linux 沙箱)
# 前提: 已从华为云下载 .NET 8 参考程序集到 /tmp/refs/
#       MCLCS.Core 已编译: dotnet build src/MCLCS.Core

set -e

CSC=/usr/share/dotnet/sdk/6.0.301/Roslyn/bincore/csc.dll
REFS_WD=/tmp/refs/wd/ref/net8.0
REFS_NC=/tmp/refs/nc/ref/net8.0
CORE_DLL=src/MCLCS.Core/bin/Debug/net6.0/MCLCS.Core.dll
OUT=/tmp/MCLCS.App.dll

# 1. 生成 XAML 桩
python3 -c "
import os, re
with open('/tmp/xaml_stub.cs','w') as out:
    seen=set()
    for r,d,fs in os.walk('src/MCLCS.App'):
        for f in fs:
            if not f.endswith('.xaml'): continue
            c=open(os.path.join(r,f)).read()
            m=re.search(r'x:Class=\"([^\"]+)\"',c)
            if not m: continue
            fn=m.group(1)
            if fn in seen: continue
            seen.add(fn)
            p=fn.split('.')
            out.write(f'namespace {\".\".join(p[:-1])} {{\n')
            out.write(f'    public partial class {p[-1]} {{\n')
            for n in re.findall(r'x:Name=\"([^\"]+)\"',c):
                out.write(f'        internal dynamic {n};\n')
            out.write(f'        public void InitializeComponent(){{}}\n')
            out.write(f'    }}\n}}\n')
"

# 2. 构建参数文件
{
  echo "-target:library"; echo "-langversion:latest"; echo "-nullable:enable"
  echo "-out:$OUT"
  find "$REFS_WD" -name "*.dll" | while read d; do echo "-r:$d"; done
  find "$REFS_NC" -name "*.dll" | while read d; do echo "-r:$d"; done
  echo "-r:$CORE_DLL"
  find src/MCLCS.App -name "*.cs" | sort | while read f; do echo "$f"; done
  echo "/tmp/globalusings.cs"
  echo "/tmp/xaml_stub.cs"
} > /tmp/build.rsp

# 3. 编译
dotnet "$CSC" @/tmp/build.rsp

echo "=== Done: $OUT ==="
ls -la "$OUT"
