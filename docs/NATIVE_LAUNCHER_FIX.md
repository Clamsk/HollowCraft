# 独立版启动器修复 / Native launcher fix — 0.40.0-alpha.2

本版修复“继续冒险”后的重复错误弹窗、空值异常和空洞窗口被隐藏的问题。游戏已经启动时，再次点击会核对进程身份并恢复已有窗口，不会再启动第二份游戏。新启动的游戏正常显示窗口。后台安装和辅助脚本继续隐藏运行。

启动器在处理辅助脚本退出结果之前先清除该进程引用，结果只消费一次；错误显示在界面内，不使用会触发定时器重入的模态弹窗。运行过程中禁用重复操作，UTF-8 日志保留中文，并通过独占启动锁防止多个启动器同时创建游戏进程。

你本机脚本更新后，关闭旧版启动器及其错误框，重新运行 **Start HollowCraft Native.cmd**，再点 **继续冒险**。如果空洞此前隐藏在后台，新入口会尝试恢复它。此次不需要重装游戏或清理存档。正在普通冒险中时，开始新的 Boss 录制需要先正常退出当前游戏。

12 项针对启动器的检查已通过，涵盖重复结果消费、回调重入、空值/轮询异常、清理异常、UTF-8 日志及 PID/启动时间/路径身份核对。本次没有启动、关闭游戏或自动游玩；此前 C# 构建和 40 项玩法规则检查的结果保持适用，玩法 DLL 没有改动。

---

This update fixes repeated error dialogs, null-reference failures after Play, and the game window starting hidden. Play now verifies the existing process and restores its Unity window instead of reporting an error or starting another copy. New interactive game windows start normally; background helpers remain hidden.

Completed helper processes are detached before their results are processed. Errors appear inside the launcher, eliminating modal timer re-entry. Action buttons stay disabled while a helper runs. UTF-8 logs preserve Chinese messages, and an exclusive launch lock prevents concurrent launcher instances from starting duplicate games.

Close the old launcher and its error dialogs, reopen **Start HollowCraft Native.cmd**, and press **Play**. An existing hidden game window will be restored if found. Reinstallation and save deletion are unnecessary. Exit a current adventure normally before starting a new Boss recording.

Twelve focused launcher checks passed. No game was launched, closed, or played automatically. The gameplay DLL is unchanged; its earlier successful build and 40 pure rule checks still apply.
