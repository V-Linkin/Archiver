# Parser 层跨平台契约

## 1. Parser 的职责

Parser 负责：
- 判断是否能解析给定 URL
- 实际抓取 URL 对应的网页/API 数据
- 从网页/API 中提取结构化内容（标题、正文、作者、媒体等）
- 展开短链（通过 HTTP 重定向）
- 下载媒体文件到本地存储

## 2. Parser 不负责什么

- **URL 平台识别** — 属于 URLNormalizer（`recognizePlatform`）
- **normalizedURL 生成** — 属于 URLNormalizer（`normalize`）
- **contentID 提取** — 属于 URLNormalizer（`extractContentID`）
- **URL 去重判断** — 属于 ImportService
- **数据库存储** — 属于 Repository
- **UI 展示** — 属于 View / ViewModel

## 3. Parser 与 URLNormalizer 的边界

| 层 | 职责 | 网络请求 | 关键输入 | 关键输出 |
|---|------|---------|---------|---------|
| URLNormalizer | 平台识别、normalizedURL、contentID | ❌ 无 | URL 字符串 | Platform? / String / String? |
| Parser | 抓取内容、解析字段、展开短链、下载媒体 | ✅ 有 | URL | ParsedContent / [MediaAsset] |

**关键规则**：
- URLNormalizer 只做字符串处理，不做任何网络请求
- 短链的 HTTP 展开属于 Parser 层（例如 `BilibiliParser.resolveShortURL`）
- `b23.tv` 和 `xhslink.com` 可被 URLNormalizer 识别平台，但短链展开和内容解析属于 Parser

## 4. Parser 输入规范

```
输入：URL（Foundation URL 对象）
前提：URL 已通过 URLNormalizer.isValidURL 校验（scheme 为 http/https）
前提：URL 已通过 PlatformRouter 路由到正确的 Parser
```

## 5. Parser 输出规范

### 5.1 成功输出

```
ParsedContent 结构体，包含：
- title: String? — 内容标题
- body: String? — 正文内容
- author: String? — 作者显示名
- authorID: String? — 作者唯一标识
- publishDate: Date? — 发布时间
- coverURL: String? — 封面图远程 URL
- imageURLs: [String] — 正文图片远程 URL 列表
- videoURL: String? — 视频远程 URL
- platformContentID: String? — 平台内容 ID
- rawMetadata: [String: String] — 平台专属扩展数据
```

### 5.2 失败输出

抛出 `ParserError`：
- `invalidURL` — URL 格式不正确
- `unsupportedPlatform` — 不支持的平台
- `parseFailed(reason: String)` — 解析失败，附带原因
- `mediaDownloadFailed(reason: String)` — 媒体下载失败
- `networkError(underlying: Error)` — 网络错误

## 6. ParsedContent 字段说明

### 6.1 跨平台核心字段

以下字段在所有平台上语义一致，Windows 端必须保持相同行为：

| 字段 | 语义 | 一致性要求 |
|------|------|-----------|
| `title` | 内容标题或正文前 50-80 字 | 可为空 |
| `body` | 正文内容（纯文本或 Markdown） | 可为空 |
| `author` | 作者显示名 | 可为空 |
| `platformContentID` | 与 URLNormalizer 提取的 contentID 一致 | 关键字段 |

### 6.2 平台扩展字段

以下字段在不同平台可能有不同填充策略：

| 字段 | 说明 |
|------|------|
| `authorID` | 仅部分平台填充（X、Weibo、Zhihu、GitHub） |
| `publishDate` | 仅部分平台填充 |
| `coverURL` | 部分平台从 og:image 或 JSON API 获取 |
| `videoURL` | 仅视频平台填充 |
| `rawMetadata` | 平台专属数据，不可作为跨平台核心依赖 |

### 6.3 ParsedContent 是运行时模型

- ParsedContent 是运行时解析结果，不是数据库持久化模型
- ParsedContent 后续会被 ImportService 转换为 Item / MediaAsset
- ParsedContent 不应直接存储到数据库
- rawMetadata 是平台专属扩展字段，只能作为附加信息

## 7. 成功/失败语义

### 成功
- 返回完整的 ParsedContent
- title 和 body 至少有一个非空（否则抛出 parseFailed）
- platformContentID 必须填充（如果能提取）

### 失败
- 抛出 ParserError，附带具体原因
- 不返回空的 ParsedContent（避免创建无内容的数据库记录）
- 网络超时/连接失败应归类为 networkError

## 8. 短链展开规则

### 8.1 当前支持的短链

| 短链域名 | 展开平台 | 展开方式 |
|---------|---------|---------|
| `b23.tv` | Bilibili | HTTP HEAD → Location 重定向 |
| `xhslink.com` | 小红书 | 仅识别平台，展开依赖 Parser/WebView 路径 |

### 8.2 不支持的短链

- `t.cn`（微博短链）— 当前不支持
- `dwz.cn`、`suo.im` 等 — 当前不支持

### 8.3 展开实现

- 使用 HTTP HEAD 请求获取 `Location` 响应头
- 不执行 GET 请求（避免下载完整页面）
- 展开后的 URL 用于提取 content ID
- 展开失败应 fallback 到原始 URL

## 9. HTTP 解析规则

### 9.1 通用 HTTP 策略

- 使用标准 URLSession（iOS/macOS）或 HttpClient（Windows）
- 默认 User-Agent: Chrome 120 on macOS
- 部分平台使用移动端 UA（微博、知乎、豆瓣、小红书）

### 9.2 数据提取优先级

大多数平台遵循以下优先级：

```
1. JSON API（如果平台提供）
2. SSR 数据（__INITIAL_STATE__、window._ROUTER_DATA 等）
3. Open Graph meta 标签
4. HTML 正则提取
5. WebView 降级（如果适用）
```

## 10. WebView 降级规则

### 10.1 何时需要 WebView

当 HTTP 请求无法获取完整渲染内容时，需要 WebView 执行 JavaScript：
- 页面内容由客户端 JS 渲染（SPA）
- 需要通过反爬挑战页
- SSR 数据不完整

### 10.2 macOS 当前实现

- 使用 WKWebView（`JSWebLoader`）
- 执行 JavaScript 提取页面内容
- 15 秒超时 + 轮询等待页面就绪
- 支持平台专属 JS 提取逻辑

### 10.3 Windows 实现要求

- 使用 WebView2 或等价方案
- JavaScript 提取脚本需要适配
- DOM 选择器可能不同（平台改版时）
- 不可直接复用 Swift/WKWebView 代码

### 10.4 WebView 与 Parser 的关系

- WebView 是 Parser 的实现细节，不是 Parser 协议的一部分
- 只能共享"策略和测试期望"，不能共享 WKWebView 实现
- 平台专属 JS 选择器是平台实现，不保证跨平台一致

## 11. 媒体 URL 提取规则

### 11.1 封面图

- 优先从 JSON API 获取高质量封面
- 回退到 og:image meta 标签
- 存储为 `cover.jpg`

### 11.2 正文图片

- 从 JSON API 或 HTML img 标签提取
- 保持原始 URL（不转换格式）
- 文件命名：`image_001.jpg`、`image_002.jpg`...

### 11.3 视频

- 优先获取最高质量源（如 bilibili 的 h264/h265）
- 去水印处理（如抖音的 `/playwm/` → `/play/`）
- 存储为 `video.mp4`

### 11.4 封面去重

- 如果封面 URL 等于首张图片 URL，从图片列表中移除首张
- 避免重复下载同一图片

## 12. rawMetadata 使用规则

### 12.1 当前各平台的 rawMetadata

| 平台 | rawMetadata 内容 |
|------|-----------------|
| GitHub | stars, forks, language |
| X | likes, retweets, replies, bookmarks, views, avatarURL, screenName |
| Weibo | type: "status" |
| YouTube | type, parseMethod, subscriberCount (channel) |
| Zhihu | type, parseMethod |
| Douban | type, rating, parseMethod |

### 12.2 使用限制

- rawMetadata 是平台专属扩展字段
- 跨平台核心逻辑不应依赖 rawMetadata
- Windows 端可以填充相同的 rawMetadata 键值
- 但不保证所有平台的所有键值在跨平台间一致

## 13. Windows 端实现要求

### 13.1 必须保持一致的行为

- 相同 URL 应解析出相同的 title、body、author
- 相同 URL 应提取相同的 platformContentID
- 相同 URL 应提取相同的媒体 URL
- 解析策略优先级应与 macOS 一致

### 13.2 不可直接复用的实现

- WKWebView → 需要用 WebView2 重新实现
- JSWebLoader → 需要用 WebView2 CDP 或等价方案
- JavaScript 提取脚本中的 DOM 选择器 → 可能需要适配
- URLSession 配置细节 → HttpClient 等价配置

### 13.3 可以直接复用的实现

- URLNormalizer 的正则表达式（标准正则，跨语言可复用）
- HTTP 请求策略（URL → API → SSR → meta → HTML）
- 短链 HTTP HEAD 展开逻辑
- ParsedContent 字段语义

## 14. 跨平台一致性要求

### 14.1 必须一致的输出

- `platformContentID` — 影响去重和导入判断
- `title` / `body` — 影响搜索和展示
- `author` — 影响展示和搜索
- 媒体 URL — 影响下载和存储

### 14.2 允许不一致的输出

- `rawMetadata` 的具体键值
- WebView 降级路径的实现细节
- User-Agent 字符串的具体内容
- HTTP 请求头的细节

### 14.3 测试验证

- Windows 端必须通过 Phase 3C 的 fixtures 测试
- 相同 URL 的核心输出必须与 macOS 完全一致
- 允许 rawMetadata 有细微差异

## 15. 平台风险分级

### 15.1 低风险

| 平台 | 原因 |
|------|------|
| Bilibili | 纯 AJAX API，无 WebView 降级 |
| GitHub | 纯 HTTP + meta 标签 + 正则，无 WebView |

### 15.2 中风险

| 平台 | 原因 |
|------|------|
| Douyin | HTTP 为主，SSR 数据提取依赖页面结构 |
| YouTube | HTTP 为主，ytInitialPlayerResponse 提取依赖页面结构 |
| X | 依赖第三方 API（fxtwitter），API 稳定性不可控 |
| Weibo | HTTP 为主，移动端 AJAX + 桌面端 meta 降级 |

### 15.3 高风险

| 平台 | 原因 |
|------|------|
| Xiaohongshu | WKWebView 是核心降级路径，DOM/挑战页变化影响解析 |
| Coolapk | 镜像站 + WKWebView 降级，镜像站可用性不可控 |
| Zhihu | API + WKWebView 降级，api.zhihu.com 稳定性不可控 |
| Douban | WebView 是核心路径，2 秒限频，__NEXT_STATE__ + JS 提取依赖 DOM，反爬风险最高 |

### 15.4 风险说明

- **低风险**：主要依赖 HTTP/API/meta，WebView 不参与核心路径
- **中风险**：HTTP 为主，但依赖第三方 API、页面结构变化、特殊 UA 或平台接口稳定性
- **高风险**：WebView/JS 降级重要，DOM 结构/挑战页/反爬策略变化会影响解析

## 16. 第三方依赖（不稳定）

当前实现依赖以下第三方服务，不保证长期稳定：

| 服务 | 使用平台 | 说明 |
|------|---------|------|
| api.fxtwitter.com | X | 非官方 Twitter 数据 API |
| api.zhihu.com | Zhihu | 知乎 API，可能需要登录态 |
| coolapk1s.com | Coolapk | 镜像站，非官方 |
| bilibili.com AJAX API | Bilibili | B站 AJAX API，接口可能变化 |

这些属于当前实现事实，不代表长期稳定契约。
