"""成功ケースと、通信違反で失敗するケースを結果ファイルから検証する。"""
from pathlib import Path
import json
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1])
expected = sys.argv[2]
tree = ET.parse(root / "results/tests.trx")
rows = tree.findall(".//{*}UnitTestResult")
audit = json.loads((root / "audit/network-audit.json").read_text(encoding="utf-8-sig"))
assert audit["network_namespace"] != audit["parent_network_namespace"], audit
assert not audit["unassigned"], audit
if expected == "passed":
    assert len(rows) >= 4 and all(r.attrib["outcome"] == "Passed" for r in rows)
    assert len(audit["cases"]) == len(rows) and all(not c["violations"] for c in audit["cases"])
else:
    assert len(rows) == 1 and rows[0].attrib["outcome"] == "Failed"
    assert sys.argv[3] in rows[0].attrib["testName"]
    assert "想定外の通信要求" in "".join(rows[0].itertext())
    assert any(c["violations"] for c in audit["cases"]), audit
print(f"検証済み: {expected}, {len(rows)}ケース, 独立したネットワーク名前空間")
