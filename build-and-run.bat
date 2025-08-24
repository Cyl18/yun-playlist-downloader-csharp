@echo off
REM 构建项目
echo 正在构建项目...
dotnet build --configuration Release

if %ERRORLEVEL% neq 0 (
    echo 构建失败！
    pause
    exit /b 1
)

echo 构建成功！

REM 运行示例
echo.
echo 使用示例：
echo 1. 基本用法：
echo    dotnet run --project src\YunPlaylistDownloader -- "https://music.163.com/#/playlist?id=123456"
echo.
echo 2. 使用歌单ID：
echo    dotnet run --project src\YunPlaylistDownloader -- 123456
echo.
echo 3. 自定义参数：
echo    dotnet run --project src\YunPlaylistDownloader -- 123456 --concurrency 10 --quality 320
echo.
echo 4. 查看帮助：
echo    dotnet run --project src\YunPlaylistDownloader -- --help
echo.

pause
