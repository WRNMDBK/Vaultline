# Vaultline Android 1.0.0

原生 Java + Android framework，无网络权限与第三方运行依赖。最低 Android 8.0，目标 Android 14（API 34）。

从 Google 官方 SDK repository 下载并校验了 Build Tools 34.0.0 和 Platform 34-ext12。运行 `build.ps1` 使用 Java 编译器、AAPT2、D8、zipalign、apksigner 生成正式签名的独立 APK。无需 Gradle 或 Android Studio。

签名私钥和密码位于 `signing`，不包含在 APK 中，也已排除于版本控制之外。务必私下备份这两个文件，未来升级必须沿用同一签名身份；不要随源代码公开分享。

私有保险库在应用内部 files 目录的 `vault.jx`。禁用系统备份和设备迁移，并阻止截图和最近任务预览泄露内容。退出前台或闲置 5 分钟会锁定，系统文件选择器期间暂缓后台锁定。

支持目录、JSON 账号、路径与字段搜索、按行复制、全量 JSON 导入导出、加密 .jx 备份恢复及主密码修改。JSON 导出和 .jx 加密格式均与桌面应用兼容。JSON 导出为明文；卸载应用会删除本机私有数据，应先备份。

当前 APK 完成编译、签名/对齐/清单验证以及核心逻辑的 JVM 测试；最终的 ColorOS 安装与交互验收需要实际手机。构建过程不读取用户真实保险库。
