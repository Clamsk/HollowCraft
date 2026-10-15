param([switch]$BossTab,[switch]$ListBosses,[switch]$ValidateOnly)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'hollowcraft-common.ps1')
$rootTask=Split-Path $PSScriptRoot -Parent
$catalogTask=Get-Content -LiteralPath (Join-Path $rootTask 'release\bosses.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($catalogTask.schema -ne 1) {throw 'Unknown boss catalogue version.'}
if($ListBosses) {$catalogTask.items | Select-Object id,zh,en,godhome;return}
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$runtimeTask=Join-Path $rootTask 'runtime'
New-Item -ItemType Directory -Path $runtimeTask -Force | Out-Null
$launcherLogTask=Join-Path $runtimeTask 'launcher.stderr.log'
$backgroundTask=[Drawing.Color]::FromArgb(17,24,31)
$panelColorTask=[Drawing.Color]::FromArgb(29,40,50)
$textColorTask=[Drawing.Color]::FromArgb(234,240,245)
$mutedTask=[Drawing.Color]::FromArgb(164,180,194)
$accentTask=[Drawing.Color]::FromArgb(87,211,147)
$formTask=New-Object Windows.Forms.Form
$formTask.Text='HollowCraft 启动器 / Launcher';$formTask.ClientSize=New-Object Drawing.Size(830,590)
$formTask.MinimumSize=New-Object Drawing.Size(846,629);$formTask.MaximumSize=$formTask.MinimumSize
$formTask.StartPosition='CenterScreen';$formTask.BackColor=$backgroundTask;$formTask.ForeColor=$textColorTask
$formTask.Font=New-Object Drawing.Font('Microsoft YaHei UI',10)
$formTask.MaximizeBox=$false
function LabelTask([string]$Text,[int]$X,[int]$Y,[int]$W,[int]$H,[int]$Size=10) {
    $labelTask=New-Object Windows.Forms.Label;$labelTask.Text=$Text
    $labelTask.Location=New-Object Drawing.Point($X,$Y);$labelTask.Size=New-Object Drawing.Size($W,$H)
    $labelTask.Font=New-Object Drawing.Font('Microsoft YaHei UI',$Size);$labelTask.ForeColor=$textColorTask
    return $labelTask
}
function ButtonTask([string]$Text,[int]$X,[int]$Y,[int]$W,[int]$H,[bool]$Primary=$false) {
    $buttonTask=New-Object Windows.Forms.Button;$buttonTask.Text=$Text
    $buttonTask.Location=New-Object Drawing.Point($X,$Y);$buttonTask.Size=New-Object Drawing.Size($W,$H)
    $buttonTask.FlatStyle='Flat';$buttonTask.FlatAppearance.BorderSize=0
    $buttonTask.BackColor=if($Primary){$accentTask}else{$panelColorTask}
    $buttonTask.ForeColor=if($Primary){$backgroundTask}else{$textColorTask};$buttonTask.Cursor='Hand'
    return $buttonTask
}
$titleTask=LabelTask 'HollowCraft' 28 22 440 42 25
$subtitleTask=LabelTask 'Minecraft × Hollow Knight  ·  0.30.17' 30 71 650 28 11
$subtitleTask.ForeColor=$mutedTask
$formTask.Controls.AddRange(@($titleTask,$subtitleTask))
$tabsTask=New-Object Windows.Forms.TabControl;$tabsTask.Location=New-Object Drawing.Point(28,113)
$tabsTask.Size=New-Object Drawing.Size(774,342);$tabsTask.Font=$formTask.Font
$normalTabTask=New-Object Windows.Forms.TabPage;$normalTabTask.Text='普通游玩 / Play';$normalTabTask.BackColor=$panelColorTask
$bossTabTask=New-Object Windows.Forms.TabPage;$bossTabTask.Text='Boss 录制 / Recording';$bossTabTask.BackColor=$panelColorTask
$tabsTask.TabPages.AddRange(@($normalTabTask,$bossTabTask));$formTask.Controls.Add($tabsTask)
$normalTabTask.Controls.Add((LabelTask '继续你的 HollowCraft 冒险' 24 24 660 40 18))
$normalTabTask.Controls.Add((LabelTask '使用现有试玩存档、背包和进度。两款游戏会自动启动。' 24 77 700 50 11))
$playTask=ButtonTask '启动普通游玩 / Play' 24 145 320 50 $true;$normalTabTask.Controls.Add($playTask)
$normalTabTask.Controls.Add((LabelTask 'A / D 移动 · 空格跳跃 · 鼠标操作 · E 背包 · F 副手' 24 231 690 30))
$searchLabelTask=LabelTask '查找 Boss / Search' 22 17 170 26
$searchTask=New-Object Windows.Forms.TextBox;$searchTask.Location=New-Object Drawing.Point(190,14);$searchTask.Size=New-Object Drawing.Size(545,28)
$searchTask.BackColor=$backgroundTask;$searchTask.ForeColor=$textColorTask;$searchTask.BorderStyle='FixedSingle'
$bossesTask=New-Object Windows.Forms.ComboBox;$bossesTask.Location=New-Object Drawing.Point(22,55)
$bossesTask.Size=New-Object Drawing.Size(714,30);$bossesTask.DropDownStyle='DropDownList'
$bossesTask.DisplayMember='Title';$bossesTask.BackColor=$backgroundTask;$bossesTask.ForeColor=$textColorTask
$levelsTask=New-Object Windows.Forms.ComboBox;$levelsTask.Location=New-Object Drawing.Point(22,105)
$levelsTask.Size=New-Object Drawing.Size(350,30);$levelsTask.DropDownStyle='DropDownList'
$null=$levelsTask.Items.AddRange(@('调谐 / Attuned','进升 / Ascended','辐辉 / Radiant'));$levelsTask.SelectedIndex=0
$resolutionTask=New-Object Windows.Forms.ComboBox;$resolutionTask.Location=New-Object Drawing.Point(390,105)
$resolutionTask.Size=New-Object Drawing.Size(346,30);$resolutionTask.DropDownStyle='DropDownList'
$null=$resolutionTask.Items.AddRange(@('1920 × 1080 / 1080p','1280 × 720 / 720p'));$resolutionTask.SelectedIndex=0
$kitTask=LabelTask '生存模式 · 全铁套 · 完整初始装备 · 每场重置' 22 154 714 26 11
$kitTask.ForeColor=$accentTask
$recordTask=ButtonTask '开始 / 切换 Boss' 22 197 350 47 $true
$hubTask=ButtonTask '返回神居大厅 / Hall of Gods' 390 197 346 47
$bossTabTask.Controls.AddRange(@($searchLabelTask,$searchTask,$bossesTask,$levelsTask,$resolutionTask,$kitTask,$recordTask,$hubTask))
$bossTabTask.Controls.Add((LabelTask 'F5 重打当前 Boss · F6 回大厅 · F8 隐藏画面提示' 22 260 714 27))
$bossTabTask.Controls.Add((LabelTask '录制使用独立存档；录屏请使用你自己的录屏工具。' 22 290 714 26))
$statusTask=LabelTask '就绪 / Ready' 28 469 774 43 10;$statusTask.ForeColor=$mutedTask;$formTask.Controls.Add($statusTask)
$installTask=ButtonTask '安装 / 配置' 28 526 225 40
$stopTask=ButtonTask '停止并保存 / Stop & Save' 269 526 285 40
$releaseTask=ButtonTask '下载更新 / Updates' 570 526 232 40
$formTask.Controls.AddRange(@($installTask,$stopTask,$releaseTask))
$script:helperTask=$null;$script:helperKindTask='';$script:recordMapTask=@()
function OwnedGameTask {
    $pathTask=Join-Path $runtimeTask 'hollow-knight-process.json'
    if(-not(Test-Path -LiteralPath $pathTask)) {return $null}
    $recordTaskInfo=Get-Content -LiteralPath $pathTask -Raw -Encoding UTF8 | ConvertFrom-Json
    $processTask=Get-Process -Id $recordTaskInfo.id -ErrorAction SilentlyContinue
    if(-not $processTask) {return $null}
    $expectedTask=Join-Path $rootTask 'runtime\Hollow Knight\hollow_knight.exe'
    if($processTask.Path -ne $expectedTask -or $processTask.StartTime.ToUniversalTime().Ticks -ne $recordTaskInfo.startedTicks) {throw 'Game process ownership mismatch.'}
    return $recordTaskInfo
}
function SetRequestTask([string]$Action,[string]$Boss,[int]$Level,[string]$Session) {
    $pathTask=Join-Path $runtimeTask 'boss-request.json';$tempTask=$pathTask+'.tmp'
    @{schema=1;session=$Session;request=[Guid]::NewGuid().ToString('N');action=$Action;boss=$Boss;level=$Level} | ConvertTo-Json | Set-Content -LiteralPath $tempTask -Encoding UTF8
    if(Test-Path -LiteralPath $pathTask) {[IO.File]::Replace($tempTask,$pathTask,$null)}else{[IO.File]::Move($tempTask,$pathTask)}
}
function HelperTask([string]$Script,[string[]]$Extra,[string]$Kind) {
    if($script:helperTask -and -not $script:helperTask.HasExited) {return}
    $pathTask=Join-Path $PSScriptRoot $Script
    $argumentsTask=@('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$pathTask+'"'))+$Extra
    $script:helperTask=Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList $argumentsTask -WorkingDirectory $rootTask -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeTask 'launcher.stdout.log') -RedirectStandardError $launcherLogTask -PassThru
    $script:helperKindTask=$Kind;$statusTask.Text='正在处理 / Working…'
}
function FailTask([object]$Error) {
    $statusTask.Text=$Error.ToString()
    [Windows.Forms.MessageBox]::Show($Error.ToString(),'HollowCraft',[Windows.Forms.MessageBoxButtons]::OK,[Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
}
function FilterTask {
    $queryTask=$searchTask.Text.Trim();$script:recordMapTask=@()
    $bossesTask.Items.Clear()
    foreach($itemTask in $catalogTask.items) {
        if($queryTask -and ($itemTask.zh+' '+$itemTask.en+' '+$itemTask.id).IndexOf($queryTask,[StringComparison]::OrdinalIgnoreCase) -lt 0) {continue}
        $displayTask=[pscustomobject]@{Title=($itemTask.zh+' / '+$itemTask.en);Boss=$itemTask}
        $script:recordMapTask+=,$displayTask;$null=$bossesTask.Items.Add($displayTask)
    }
    if($bossesTask.Items.Count) {
        $bossesTask.SelectedIndex=0
        if(-not $queryTask) {for($iTask=0;$iTask -lt $bossesTask.Items.Count;$iTask++) {if($bossesTask.Items[$iTask].Boss.id -eq 'GG_Hornet_1') {$bossesTask.SelectedIndex=$iTask;break}}}
    }
    $recordTask.Enabled=$bossesTask.Items.Count -gt 0
}
$searchTask.Add_TextChanged({FilterTask})
$bossesTask.Add_SelectedIndexChanged({
    if($bossesTask.SelectedItem) {$levelsTask.Enabled=[bool]$bossesTask.SelectedItem.Boss.godhome;if(-not $levelsTask.Enabled) {$levelsTask.SelectedIndex=0}}
})
$playTask.Add_Click({try {if(OwnedGameTask) {throw 'HollowCraft 已在运行，请先停止 / Stop the current game first.'};HelperTask 'start-hollowcraft.ps1' @() 'start';$formTask.WindowState='Minimized'}catch{FailTask $_}})
$recordTask.Add_Click({try {
    if(-not $bossesTask.SelectedItem) {return}
    $chosenTask=$bossesTask.SelectedItem.Boss;$ownedTask=OwnedGameTask
    if($ownedTask) {
        if($ownedTask.mode -ne 'boss') {throw '请先停止普通游玩，再启动独立录制 / Stop normal play before recording.'}
        SetRequestTask 'boss' $chosenTask.id $levelsTask.SelectedIndex $ownedTask.session
    }else{
        $widthTask=if($resolutionTask.SelectedIndex -eq 0){1920}else{1280};$heightTask=if($resolutionTask.SelectedIndex -eq 0){1080}else{720}
        HelperTask 'start-hollowcraft.ps1' @('-BossId',$chosenTask.id,'-BossLevel',[string]$levelsTask.SelectedIndex,'-Width',[string]$widthTask,'-Height',[string]$heightTask) 'start'
    }
    $statusTask.Text='正在进入 Boss；首次启动需要等待游戏加载 / Loading boss…';$formTask.WindowState='Minimized'
}catch{FailTask $_}})
$hubTask.Add_Click({try {$ownedTask=OwnedGameTask;if(-not $ownedTask -or $ownedTask.mode -ne 'boss') {throw '请先启动 Boss 录制 / Start a recording session first.'};SetRequestTask 'hub' '' 0 $ownedTask.session}catch{FailTask $_}})
$stopTask.Add_Click({try {HelperTask 'stop-hollowcraft.ps1' @() 'stop'}catch{FailTask $_}})
$releaseTask.Add_Click({Start-Process 'https://github.com/Clamsk/HollowCraft/releases' | Out-Null})
$installTask.Add_Click({try {Start-Process -FilePath (Join-Path $rootTask 'Install HollowCraft.cmd') -WorkingDirectory $rootTask -WindowStyle Normal | Out-Null}catch{FailTask $_}})
$timerTask=New-Object Windows.Forms.Timer;$timerTask.Interval=800
$timerTask.Add_Tick({try {
    if($script:helperTask -and $script:helperTask.HasExited) {
        if($script:helperTask.ExitCode -ne 0) {$statusTask.Text=(Get-Content -LiteralPath $launcherLogTask -Raw -ErrorAction SilentlyContinue)}
        elseif($script:helperKindTask -eq 'stop') {$statusTask.Text='已保存并停止 / Saved and stopped'}
        $script:helperTask.Dispose();$script:helperTask=$null
    }
    $ownedTask=OwnedGameTask
    $pathTask=Join-Path $runtimeTask 'boss-status.json'
    if($ownedTask -and $ownedTask.mode -eq 'boss' -and (Test-Path -LiteralPath $pathTask)) {
        $liveTask=Get-Content -LiteralPath $pathTask -Raw -Encoding UTF8 | ConvertFrom-Json
        if($liveTask.session -eq $ownedTask.session) {$statusTask.Text=$liveTask.message}
    }
}catch{$statusTask.Text=$_.Exception.Message}})
FilterTask
if($BossTab) {$tabsTask.SelectedTab=$bossTabTask}
$formTask.Add_Shown({$timerTask.Start()})
$formTask.Add_FormClosed({$timerTask.Stop();$timerTask.Dispose()})
if($ValidateOnly) {
    Write-Output ('Launcher initialized without showing UI or starting games: '+$bossesTask.Items.Count+' bosses.')
    $timerTask.Dispose();$formTask.Dispose();return
}
[Windows.Forms.Application]::Run($formTask)
