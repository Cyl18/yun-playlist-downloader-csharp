#!/bin/bash

# 构建项目
echo "正在构建项目..."
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo "构建失败！"
    exit 1
fi

echo "构建成功！"

# 运行示例
echo ""
echo "使用示例："
echo "1. 基本用法："
echo "   dotnet run --project src/YunPlaylistDownloader -- \"https://music.163.com/#/playlist?id=123456\""
echo ""
echo "2. 使用歌单ID："
echo "   dotnet run --project src/YunPlaylistDownloader -- 123456"
echo ""
echo "3. 自定义参数："
echo "   dotnet run --project src/YunPlaylistDownloader -- 123456 --concurrency 10 --quality 320"
echo ""
echo "4. 查看帮助："
echo "   dotnet run --project src/YunPlaylistDownloader -- --help"
echo ""
