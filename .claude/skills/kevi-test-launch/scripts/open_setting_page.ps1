# KEVi の設定ウィンドウを歯車ボタンから開き、指定したカテゴリ配下のページを UIA でクリックして撮影する
# 使い方: .\open_setting_page.ps1 -AppPid <pid> -OutDir <dir> -Category 強震モニタ -PageName EEW\ Client連携
param([int]$AppPid, [string]$OutDir, [string]$Category = "強震モニタ", [string]$PageName = "外部アプリ連携")
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Collections.Generic; using System.Text;
public class W {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L, T, Rt, B; }
  public static List<IntPtr> Find(uint pid) { var l = new List<IntPtr>(); EnumWindows((h, _) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) l.Add(h); return true; }, IntPtr.Zero); return l; }
  public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(100); mouse_event(2, 0, 0, 0, IntPtr.Zero); mouse_event(4, 0, 0, 0, IntPtr.Zero); }
}
"@
function Shot($h, $name) {
  $r = New-Object W+R; [W]::GetWindowRect($h, [ref]$r) | Out-Null
  $bmp = New-Object System.Drawing.Bitmap ($r.Rt - $r.L), ($r.B - $r.T); $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size); $bmp.Save("$OutDir\$name.png"); $g.Dispose(); $bmp.Dispose()
}
function FindByName($root, $name) {
  $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
  return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}
function ClickElement($el) {
  $rc = $el.Current.BoundingRectangle
  [W]::Click([int]($rc.X + $rc.Width / 2), [int]($rc.Y + $rc.Height / 2))
}

$main = (Get-Process -Id $AppPid).MainWindowHandle
[W]::SetForegroundWindow($main) | Out-Null; Start-Sleep -Milliseconds 500
$mr = New-Object W+R; [W]::GetWindowRect($main, [ref]$mr) | Out-Null
# 左下の歯車ボタン
[W]::Click($mr.L + 40, $mr.B - 48)
Start-Sleep -Seconds 3
$wins = [W]::Find([uint32]$AppPid) | Where-Object { $_ -ne $main }
"windows: " + (($wins | ForEach-Object { [W]::Title($_) }) -join " / ")
$settings = $wins | Select-Object -First 1
if (-not $settings) { throw "settings window not found" }
[W]::SetForegroundWindow($settings) | Out-Null; Start-Sleep -Milliseconds 500
$root = [System.Windows.Automation.AutomationElement]::FromHandle($settings)

$km = FindByName $root $Category
if ($km) { ClickElement $km; Start-Sleep -Seconds 1 }
$item = FindByName $root $PageName
"item found: " + ($null -ne $item)
if ($item) { ClickElement $item; Start-Sleep -Seconds 2 }
Shot $settings "settings_page"
"done"
