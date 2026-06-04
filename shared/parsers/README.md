# shared/parsers — Parser 跨平台契约

本目录定义了解析器层的跨平台契约，供 macOS 和 Windows 端共同遵守。

## 文件说明

| 文件 | 用途 |
|------|------|
| `parser-contract.md` | Parser 层的完整契约文档 |
| `platform-parser-rules.json` | 每个平台的解析策略、依赖、输出字段定义 |

## 核心原则

1. **URL 是输入，ParsedContent 是输出** — Parser 负责将 URL 抓取并解析为结构化内容
2. **短链展开属于 Parser 层** — URLNormalizer 不做网络请求
3. **WebView 是平台实现细节** — macOS 用 WKWebView，Windows 需用 WebView2 等价方案
4. **rawMetadata 是扩展字段** — 跨平台核心字段以 ParsedContent 前 9 个字段为准
5. **第三方 API 不保证稳定** — fxtwitter、api.zhihu.com 等依赖可能随时变化

## Windows 端实现要求

- 必须保持与 macOS 相同的解析策略优先级
- 必须输出相同的 ParsedContent 字段
- WebView 降级路径需要独立实现（WebView2 / 等价方案）
- JavaScript 提取脚本需要适配（DOM 选择器可能不同）
- 短链 HTTP 展开可直接复用（标准 HTTP HEAD 请求）

## 当前支持的解析器

| 平台 | 解析器 | 风险等级 |
|------|--------|---------|
| 抖音 | DouyinParser | 中 |
| 小红书 | XiaohongshuParser | 高 |
| 酷安 | CoolapkParser | 高 |
| B站 | BilibiliParser | 低 |
| GitHub | GitHubParser | 低 |
| YouTube | YouTubeParser | 中 |
| X | XParser | 中 |
| 微博 | WeiboParser | 中 |
| 知乎 | ZhihuParser | 高 |
| 豆瓣 | DoubanParser | 高 |

## 已知限制

- t.cn 短链不支持
- 部分平台依赖第三方 API（fxtwitter、api.zhihu.com）
- 豆瓣有 2 秒限频
- 小红书/酷安/知乎/豆瓣的 WebView 降级依赖 DOM 结构，平台改版可能失效
