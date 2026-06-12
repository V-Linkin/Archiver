# 拾屿 Gatherly Windows 已知限制

> 当前稳定提交：ac48159
> 更新日期：2026-06-12

---

## 功能限制

### 1. 平台管理尚未实现

- 不能在 UI 新建自定义平台
- 不能重命名自定义平台
- 不能删除自定义平台
- 不能拖动排序平台
- 不能将内容手动移动到另一个平台
- 设置页暂无平台管理入口

### 2. merged 平台白名单当前仅 YouTube、B站

- YouTube 和 B站支持标准平台 + macOS 备份 custom 平台合并显示
- 其它标准平台（小红书、微博、知乎等）暂未统一合并
- 小红书当前走 custom 查询，显示正常

### 3. 详情页正文链接不是行内链接

- 当前在正文下方独立链接区域显示
- 正文中的 URL 可被识别和点击
- 但链接不在正文文本中直接可点击

### 4. 部分平台 Parser 仍未实现

当前已实现 Parser：
- GitHub ✅
- Bilibili ✅
- YouTube ✅

以下平台为 NotImplementedParser：
- 抖音
- 小红书
- 酷安
- X (Twitter)
- 微博
- 知乎
- 豆瓣

### 5. macOS ImportTask.swift 尚未接入 updatedAt

- shared contract 已加入 updatedAt 字段
- Windows 已使用 updated_at 进行 stale task 检测
- macOS Swift 模型后续需同步

---

## 技术限制

### 6. Self-contained 发布包

当前是 self-contained publish 包，不是正式安装器（MSI/EXE）。用户需要手动解压运行。

### 7. 数据目录固定

数据目录固定为 `%LOCALAPPDATA%\Gatherly\`，暂不支持自定义。

### 8. 不支持备份导出

目前只支持从 macOS 导入备份，不支持从 Windows 导出备份。

### 9. Windows 当前未实现 WebView2 fallback

部分平台 Parser 可能需要 WebView2 作为 fallback，当前未实现。

---

## 已解决问题（历史记录）

以下问题已在 Phase 7D-3 中修复：

- ~~YouTube 无法导入~~ → YouTube Parser 已实现
- ~~旧 import_task 永久阻止导入~~ → stale task 规则已实现
- ~~回收站彻底删除 FOREIGN KEY 失败~~ → FK 清理顺序已修复
- ~~YouTube 平台不显示新内容~~ → merged 查询已修复
- ~~B站备份内容不显示~~ → merged 查询已修复
- ~~小红书数量不一致~~ → count/list 一致性已修复
- ~~详情正文不能复制~~ → SelectableTextBlock 已实现
- ~~详情链接不能点击~~ → ExternalLinkService 已实现
