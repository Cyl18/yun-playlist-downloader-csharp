# YunPlaylistDownloader - C# 版本

网易云音乐 歌单/专辑/电台 下载器 - C# 重构版本

## 特性

- ✅ 支持歌单 / 专辑 / 电台
- ✅ 音质选择 (128k/192k/320k/999k)
- ✅ 下载超时 / 重试机制
- ✅ 再次下载默认跳过已下载部分
- ✅ 自定义文件名格式
- ✅ 实时下载进度显示
- ✅ Cookie 认证支持 (VIP 用户)
- ✅ 并发下载控制
- ✅ 跳过试听歌曲选项

## 技术栈

- **.NET 9.0** - 最新的 .NET 框架
- **System.CommandLine** - 现代化命令行解析
- **Spectre.Console** - 美观的控制台输出和进度条
- **Microsoft.Extensions.*** - 依赖注入、配置、日志等
- **Polly** - 重试策略和容错处理
- **Humanizer** - 人性化的时间和文件大小显示

## 安装

### 作为全局工具安装
```bash
dotnet pack
dotnet tool install -g --add-source ./nupkg YunPlaylistDownloader
```

### 本地构建运行
```bash
dotnet build
dotnet run -- <url> [options]
```

## 使用方法

```bash
# 基本用法
yun "https://music.163.com/#/playlist?id=123456"
yun 123456  # 直接使用歌单ID

# 指定参数
yun <url> --concurrency 10          # 10首同时下载
yun <url> --quality 320              # 320k音质
yun <url> --format ":singer - :songName.:ext"  # 自定义文件名
yun <url> --cookie yun.cookie.txt    # 使用Cookie文件
yun <url> --skip-trial               # 跳过试听歌曲
yun <url> --cover                    # 下载封面
```

## 参数说明

- `url` - 歌单/专辑/电台的链接或ID
- `--concurrency, -c` - 同时下载数量 (默认: 5)
- `--format, -f` - 文件名格式 (默认: ":name/:singer - :songName.:ext")
- `--quality, -q` - 音质选择: 128/192/320/999 (默认: 999)
- `--retry-timeout` - 下载超时分钟数 (默认: 3)
- `--retry-times` - 下载重试次数 (默认: 3)
- `--skip, -s` - 跳过已存在文件 (默认: true)
- `--progress, -p` - 显示进度条 (默认: true)
- `--cover` - 下载封面 (默认: false)
- `--cookie` - Cookie文件路径 (默认: "yun.cookie.txt")
- `--skip-trial` - 跳过试听歌曲 (默认: false)

## 文件名格式变量

- `:name` - 歌单/专辑名称
- `:singer` - 歌手名
- `:songName` - 歌曲名
- `:albumName` - 专辑名
- `:ext` - 文件扩展名
- `:index` - 序号

## Cookie 配置

对于VIP歌曲，需要配置Cookie：

1. 创建 `yun.cookie.txt` 文件
2. 从浏览器复制网易云音乐的Cookie
3. 粘贴到文件中（支持注释，以//开头的行会被忽略）

## 开发

### 项目结构
```
src/YunPlaylistDownloader/
├── Program.cs              # 程序入口
├── Models/                 # 数据模型
├── Services/               # 业务服务
├── Adapters/               # 适配器模式
├── Commands/               # 命令行处理
└── appsettings.json        # 配置文件

tests/YunPlaylistDownloader.Tests/  # 单元测试
```

### 主要类说明

- **NetEaseApiClient** - 网易云音乐API客户端
- **DownloadService** - 下载服务，支持并发和重试
- **ProgressService** - 进度显示服务
- **CookieService** - Cookie管理
- **AdapterFactory** - 适配器工厂，支持歌单/专辑/电台
- **FileNameService** - 文件名处理和sanitization

## 注意事项

- 本项目不支持越权使用
- VIP 歌曲请开通 VIP 后结合 `--cookie` 使用
- 请遵守相关法律法规，仅用于个人学习和研究

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！
