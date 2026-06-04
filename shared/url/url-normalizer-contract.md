# URL 标准化器跨平台契约

## 1. 目标

URL 标准化器（URLNormalizer）用于：
- 从混合文本中识别支持平台的 URL
- 从 URL 识别所属平台
- 将 URL 标准化为统一格式（normalizedURL），用于数据库去重
- 从 URL 提取平台内容 ID（platformContentID），用于导入重复判断

## 2. 公开方法

### 2.1 extractURLs(from:) → [String]

**输入**：包含 URL 的混合文本
**输出**：所有支持平台的 URL 数组（保持出现顺序）
**实现**：使用 `NSDataDetector`（NSTextCheckingResult.CheckingType.link）提取所有链接，再过滤出可识别平台的 URL

### 2.2 extractFirstURL(from:) → String?

**输入**：混合文本
**输出**：第一个支持平台的 URL，或 nil
**实现**：调用 `extractURLs(from:)` 取第一个

### 2.3 recognizePlatform(_:) → Platform?

**输入**：URL 字符串
**输出**：识别到的 Platform，或 nil（无法识别）
**实现**：基于域名关键词匹配（case-insensitive）

### 2.4 normalize(_:platform:) → String

**输入**：URL 字符串 + 已识别的平台
**输出**：标准化后的 URL（normalizedURL）
**实现**：按平台分发到对应的 normalize 方法，使用自定义 scheme

### 2.5 extractContentID(_:platform:) → String?

**输入**：URL 字符串 + 已识别的平台
**输出**：平台内容 ID，或 nil
**实现**：按平台分发到对应的 ID 提取方法

### 2.6 isValidURL(_:) → Bool

**输入**：URL 字符串
**输出**：是否为合法 URL（scheme 为 http 或 https）
**实现**：使用 Foundation `URL(string:)` 解析

## 3. 平台识别规则

通过域名关键词匹配（case-insensitive）：

| 平台 | 识别域名 | 关键词 |
|------|---------|--------|
| douyin | douyin.com, iesdouyin.com | `douyin.com`, `iesdouyin.com` |
| xiaohongshu | xiaohongshu.com, xhslink.com | `xiaohongshu.com`, `xhslink.com` |
| coolapk | coolapk.com, coolapk1s.com | `coolapk.com`, `coolapk1s.com` |
| bilibili | bilibili.com, b23.tv | `bilibili.com`, `b23.tv` |
| github | github.com | `github.com` |
| youtube | youtube.com, youtu.be | `youtube.com`, `youtu.be` |
| x | x.com, twitter.com | `x.com`, `twitter.com` |
| weibo | weibo.com, m.weibo.cn | `weibo.com`, `m.weibo.cn` |
| zhihu | zhihu.com | `zhihu.com` |
| douban | douban.com | `douban.com` |

**注意**：`recognizePlatform` 对 `custom` 平台始终返回 `nil`。

## 4. normalizedURL 生成规则

每个平台使用自定义 scheme URL，只包含内容 ID：

| 平台 | normalizedURL 格式 | 示例 |
|------|-------------------|------|
| douyin | `douyin://video/{id}` | `douyin://video/7301234567890` |
| xiaohongshu | `xiaohongshu://explore/{id}` | `xiaohongshu://explore/65a1b2c3d4e5f6` |
| coolapk | `coolapk://feed/{id}` | `coolapk://feed/12345678` |
| bilibili | `bilibili://video/{id}` | `bilibili://video/BV1xx411c7mD` |
| github | `github://repo/{owner/name}` | `github://repo/octocat/Hello-World` |
| youtube | `youtube://video/{id}` | `youtube://video/dQw4w9WgXcQ` |
| x | `x://tweet/{id}` | `x://tweet/1234567890` |
| weibo | `weibo://status/{id}` | `weibo://status/4892046789012` |
| zhihu | `zhihu://content/{id}` | `zhihu://content/12345678` |
| douban | `douban://subject/{id}` | `douban://subject/35517853` |
| custom | 原始 URL 不变 | — |

**关键行为**：
- 如果能提取 content ID → 生成自定义 scheme URL
- 如果不能提取 content ID → 返回原始 URL

## 5. platformContentID 提取规则

每个平台使用正则表达式提取第一个捕获组：

### 5.1 抖音 (douyin)

正则：
```
douyin\.com/video/(\d+)
iesdouyin\.com/share/video/(\d+)
```
ID 类型：纯数字

### 5.2 小红书 (xiaohongshu)

正则：
```
xiaohongshu\.com/explore/([a-f0-9]+)
xiaohongshu\.com/discovery/item/([a-f0-9]+)
```
ID 类型：十六进制字符串

### 5.3 酷安 (coolapk)

正则：
```
coolapk\.com/feed/(\d+)
coolapk1s\.com/feed/(\d+)
```
ID 类型：纯数字

### 5.4 B站 (bilibili)

正则：
```
bilibili\.com/video/(BV[a-zA-Z0-9]+)
bilibili\.com/video/(av\d+)
```
ID 类型：BV 号或 av 号

### 5.5 GitHub

正则：
```
github\.com/([a-zA-Z0-9._-]+/[a-zA-Z0-9._-]+)
```
ID 类型：`owner/name`

### 5.6 YouTube

正则：
```
youtube\.com/watch\?v=([a-zA-Z0-9_-]{11})
youtu\.be/([a-zA-Z0-9_-]{11})
youtube\.com/embed/([a-zA-Z0-9_-]{11})
youtube\.com/shorts/([a-zA-Z0-9_-]{11})
youtube\.com/channel/([a-zA-Z0-9_-]+)
youtube\.com/@([a-zA-Z0-9._-]+)
```
ID 类型：视频 ID（11 字符）或 channel handle

### 5.7 X / Twitter

正则：
```
(?:x|twitter)\.com/[^/]+/status/(\d+)
(?:x|twitter)\.com/i/status/(\d+)
```
ID 类型：纯数字 tweet ID

额外辅助方法 `extractUsername`：
```
(?:x|twitter)\.com/([a-zA-Z0-9_]+)/status/
(?:x|twitter)\.com/([a-zA-Z0-9_]+)$
```

### 5.8 微博 (weibo)

正则：
```
weibo\.com/status/(\d+)
weibo\.com/\d+/([a-zA-Z0-9]+)
m\.weibo\.cn/detail/(\d+)
m\.weibo\.cn/status/(\d+)
```
ID 类型：纯数字或字母数字混合

### 5.9 知乎 (zhihu)

正则：
```
zhihu\.com/question/\d+/answer/(\d+)
zhihu\.com/p/(\d+)
zhihu\.com/column/([a-zA-Z0-9_-]+)
```
ID 类型：纯数字（answer/post ID）或 column handle

### 5.10 豆瓣 (douban)

正则：
```
douban\.com/subject/(\d+)
```
ID 类型：纯数字

## 6. Tracking 参数处理

**当前行为**：
- URLNormalizer **不显式清理** tracking 参数（如 `utm_*`、`spm`、`from` 等）
- 对于能提取 content ID 的平台，normalizedURL 由 content ID 生成（自定义 scheme），tracking 参数不会影响去重
- 对于无法提取 content ID 的 URL（如 custom 平台），保留原始 URL，tracking 参数可能影响去重

**影响**：
- 同一抖音视频的不同分享链接（带不同 `from` 参数）→ 归一到相同 `douyin://video/{id}` → 去重正常
- 无法识别的 URL 带不同 `utm_*` 参数 → 保留原始 URL → 可能被当作不同内容

## 7. 短链接处理

### 7.1 URLNormalizer 层

URLNormalizer 可以通过域名识别以下短链平台：
- `b23.tv` → 识别为 bilibili
- `xhslink.com` → 识别为 xiaohongshu

但 URLNormalizer **不负责 HTTP 展开短链**。

### 7.2 Parser 层

短链展开属于 Parser 实现：
- `BilibiliParser` 可能需要 resolve `b23.tv` 短链
- `XiaohongshuParser` 可能需要 resolve `xhslink.com` 短链

### 7.3 已知不支持

- `t.cn`（微博短链）当前不支持
- `dwz.cn`、`suo.im` 等其他短链服务当前不支持

## 8. 移动端链接处理

URLNormalizer 对以下移动端链接有对应处理：
- `m.weibo.cn` → 识别为 weibo
- `iesdouyin.com` → 识别为 douyin

这些链接可以被 `recognizePlatform` 识别，但能否提取 content ID 取决于 URL 格式：
- `m.weibo.cn/detail/{id}` → 可提取
- `iesdouyin.com/share/video/{id}` → 可提取

## 9. Windows 端实现要求

### 9.1 必须保持一致的行为

- `recognizePlatform` 对相同 URL 必须返回相同平台
- `normalize` 对相同 URL 必须返回相同 normalizedURL
- `extractContentID` 对相同 URL 必须返回相同 contentID
- 正则表达式必须完全一致（标准正则，C# `Regex` 可直接使用）

### 9.2 不可随意修改的规则

- **normalizedURL 影响数据库去重** — 修改规则可能导致同一内容被重复导入
- **platformContentID 影响导入判断** — 修改规则可能导致导入失败
- **正则表达式跨平台一致** — Windows 端必须通过同一批测试用例

### 9.3 Windows 端实现建议

```
1. 使用 C# System.Text.RegularExpressions 实现相同的正则匹配
2. 使用 string.Contains（StringComparison.OrdinalIgnoreCase）做域名匹配
3. 使用自定义 scheme URL 做 normalizedURL
4. 通过 url-normalizer-test-cases.json 验证一致性
```

## 10. 不可修改的规则

| 规则 | 原因 |
|------|------|
| normalizedURL 格式 | 影响数据库去重，修改导致数据不一致 |
| platformContentID 提取 | 影响导入判断，修改导致导入失败 |
| 平台识别域名 | 影响 URL 路由，修改导致内容被错误分类 |
| 正则表达式 | 跨平台一致性要求 |
