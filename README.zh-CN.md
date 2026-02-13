<p align="center">
  <img width="512px" height="auto" src="./.github/assets/CollapseLauncherIdolType.png"/>
</p>

<div align="center">

# Hi3Helper.Plugin.Endfield

[English](./README.md) · **简体中文**

为 [Collapse Launcher (Collapse 启动器)](https://collapselauncher.com/) 开发的第三方插件，旨在支持 **《明日方舟：终末地》**
的下载、更新与启动。

当前插件核心功能已就绪，可正常用于日常游戏启动与管理。

<img width="80%" alt="Plugin Preview" src="https://github.com/user-attachments/assets/f3f572c0-bfb7-4436-b8e7-47765f42c052" />

</div>

<p align="center">
  <a href="https://github.com/palmcivet/awesome-arknights-endfield"><img src="https://github.com/palmcivet/awesome-arknights-endfield/blob/main/assets/badge-for-the-badge.svg" alt="Awesome Arknights Endfield badge" /></a>
  <a href="https://github.com/misaka10843/Hi3Helper.Plugin.Endfield/graphs/contributors" target="_blank"><img alt="GitHub contributors" src="https://img.shields.io/github/contributors/misaka10843/Hi3Helper.Plugin.Endfield?style=for-the-badge&logo=github"></a>
  <a href="https://github.com/misaka10843/Hi3Helper.Plugin.Endfield/stargazers" target="_blank"><img alt="GitHub Repo stars" src="https://img.shields.io/github/stars/misaka10843/Hi3Helper.Plugin.Endfield?style=for-the-badge&label=%E2%AD%90STAR"></a>
</p>


---

> [!WARNING]
> **注意事项与已知局限**
>
> 由于 Collapse Launcher 暂无官方插件开发文档，本项目功能主要参考现有插件实现，**并可能存在以下局限性**：
>
> 1. **预下载功能**：当前官方启动器还并未开启预下载功能，暂时无法获知对应的数据结构，因此无法支持。
> 2. **完整性校验**：暂不计划制作游戏完整性检查功能（这涉及逆向官方启动器逻辑）。
> 3. **更新功能风险**：游戏更新功能可能存在问题（暂时无法测试）。
     >    **强烈建议在更新时备份游戏目录，防止更新出错导致文件损坏。**

## ✨ 功能特性

### ✅ 当前已支持

- **版本检测**：自动检测客户端版本是否为最新。
- **资讯获取**：自动拉取并展示官方背景图、Banner 以及最新新闻公告。
- **游戏管理**：支持完整的游戏下载、安装、启动及运行检测。
- **多服支持**：
    - [x] 国服 (CN)
    - [x] 全球服 (Global)
    - [x] Bilibili服 (Bili)

### 🚧 开发计划 / 待办事项 (ToDo)

- [ ] **游戏更新**：待游戏正式版本（1.0.14+）发布后进行测试与适配。
- [ ] **预下载支持**：需等待官方启动器实装相关接口。
- [ ] **完整性校验**：目前Collapse启动器似乎暂未提供相关 API 接口，需等待上游更新。
- [ ] **社媒面板**：集成官方社交媒体动态展示。(现基本支持，但是因为icon无法通过api获取所以暂时关闭)

---

## 🧩 如何安装插件

**前置要求：**
在使用本插件前，请确保您的 Collapse Launcher 版本为 `1.83.14` 或更高版本。

### 安装步骤

1. **下载插件**
   前往 [Releases 页面](https://github.com/misaka10843/Hi3Helper.Plugin.Endfield/releases/latest) 下载最新的插件压缩包（
   `.zip` 文件）。

   ![Release Download Page](./.github/assets/img.png)

2. **进入插件管理**
   打开启动器，进入 **设置 (Settings)** 页面，向下滚动找到并点击 `打开插件管理菜单`。

   ![Settings Menu](./.github/assets/img_2.png)

3. **添加并应用**
   在弹出的窗口中，点击 `点击添加 .zip 或 manifest.json` 按钮，选择刚刚下载的 `.zip` 文件。

   完成添加后，**重启启动器**即可生效。

   ![Add Plugin Dialog](./.github/assets/img_1.png)

---

## ⚠️ 免责声明

本项目是第三方开源插件，与 *GRYPHLINE* 或 *Hypergryph* 无关。