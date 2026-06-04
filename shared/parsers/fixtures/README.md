# Parser Fixtures

## 目的

为 10 个平台 Parser 提供标准化测试数据，供 macOS 和 Windows 端共同验证解析一致性。

## 目录结构

```text
fixtures/
  README.md
  fixture-schema.json          — 统一 fixture 定义 schema
  bilibili/fixtures.json       — B站
  github/fixtures.json         — GitHub
  youtube/fixtures.json        — YouTube
  x/fixtures.json              — X (Twitter)
  douyin/fixtures.json         — 抖音
  weibo/fixtures.json          — 微博
  xiaohongshu/fixtures.json    — 小红书
  coolapk/fixtures.json        — 酷安
  zhihu/fixtures.json          — 知乎
  douban/fixtures.json         — 豆瓣
```

## 强断言 vs 弱断言

### 强断言（strong）

必须完全一致，跨平台保持相同结果：
- `platform` — 平台识别必须正确
- `contentID` — content ID 提取必须精确
- `normalizedURL` — normalizedURL 生成必须一致
- `parser` — 必须路由到正确的 Parser

强断言失败意味着跨平台兼容性问题。

### 弱断言（weak）

允许平台改版后失败，需人工确认：
- `titlePresent` — title 是否非空
- `bodyPresent` — body 是否非空
- `authorPresent` — author 是否非空
- `mediaPresent` — 是否有封面或图片

弱断言失败通常意味着平台页面结构变化，需要更新 Parser。

## 为什么不写死完整标题/正文/图片 URL

- 平台内容经常变化（标题修改、图片更新）
- 写死会导致 fixture 频繁失效
- 弱断言只验证"非空"，不验证"等于某个值"
- 强断言只验证平台识别和 ID 提取，这些是稳定的

## 需要网络的平台

所有平台都需要网络请求。fixture 中标记 `requiresNetwork: true`。

## 需要 WebView 的平台

以下平台可能需要 WebView 降级：
- **小红书** — HTTP SSR 失败时降级到 WKWebView
- **酷安** — 镜像站和原站都失败时降级到 WKWebView
- **知乎** — API 失败时降级到 WKWebView
- **豆瓣** — 移动端和桌面端都失败时降级到 WKWebView

Windows 端需要用 WebView2 或等价方案重新实现。

## 稳定性分级

- **stable** — 低/中风险平台，主要依赖 HTTP/API，不易因平台改版失效
- **volatile** — 高风险平台，依赖 WebView/DOM，平台改版可能导致 fixture 失效

## 短链样例

以下平台包含短链 fixture：
- **Bilibili** — `b23.tv`，Parser 通过 HTTP HEAD 展开
- **小红书** — `xhslink.com`，Parser/WebView 展开

短链的 contentID 和 normalizedURL 在展开前不可预测，因此断言为 false。

## macOS 和 Windows 如何复用

### macOS 端

1. 加载 `fixtures.json`
2. 对每条 fixture，调用 `PlatformRouter.parse(urlString:)`
3. 验证强断言（platform、contentID、normalizedURL）
4. 验证弱断言（title/body/author 非空）
5. 记录失败的 fixture，区分强断言失败和弱断言失败

### Windows 端

1. 加载相同的 `fixtures.json`
2. 用 C# 实现的 Parser 执行相同测试
3. 强断言结果必须与 macOS 一致
4. 弱断言允许有差异（平台改版、网络环境不同）

## Phase 3C 范围

本轮只提供测试数据（fixtures），不提供测试执行器。
测试执行器将在后续阶段实现。

## 平台 Fixture 数量

| 平台 | 数量 | 短链 | WebView | 稳定性 |
|------|------|------|---------|--------|
| Bilibili | 3 | ✅ b23.tv | ❌ | stable |
| GitHub | 3 | ❌ | ❌ | stable |
| YouTube | 3 | ❌ | ❌ | stable |
| X | 3 | ❌ | ❌ | stable |
| Douyin | 3 | ❌ | ❌ | stable |
| Weibo | 3 | ❌ | ❌ | stable |
| Xiaohongshu | 3 | ✅ xhslink.com | ✅ | volatile |
| Coolapk | 2 | ❌ | ✅ | volatile |
| Zhihu | 3 | ❌ | ✅ | volatile |
| Douban | 3 | ❌ | ✅ | volatile |
