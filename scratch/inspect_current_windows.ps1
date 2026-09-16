Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process -Name myaccount -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Host "myaccount process not found"
    exit
}

Write-Host "Prosoft PID: $($proc.Id)"
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
Write-Host "Found $($windows.Count) top-level UIA windows"

foreach ($w in $windows) {
    Write-Host "Window: '$($w.Current.Name)' Class: '$($w.Current.ClassName)' HWND: 0x$($w.Current.NativeWindowHandle.ToString('X'))"
    
    # Also find child elements
    $children = $w.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "  Total descendants: $($children.Count)"
    foreach ($c in $children) {
        $name = $c.Current.Name
        $type = $c.Current.ControlType.ProgrammaticName
        $cls = $c.Current.ClassName
        $rect = $c.Current.BoundingRectangle
        if ($name -or $cls -like "*Edit*" -or $cls -like "*Combo*" -or $cls -like "*Button*" -or $cls -like "*PB*") {
            Write-Host "    [$type] Name='$name' Class='$cls' Rect=$($rect.Left),$($rect.Top),$($rect.Width),$($rect.Height)"
        }
    }
}
