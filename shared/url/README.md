# shared/url — URL 标准化跨平台契约

本目录定义了 URL 标准化规则的跨平台契约，供 macOS 和 Windows 端共同遵守。

## 文件说明

| 文件 | 用途 |
|------|------|
| `url-normalizer-contract.md` | URL 标准化规则的完整契约文档 |
| `url-normalizer-rules.json` | 可配置化的平台识别和 URL 标准化规则 |
| `url-normalizer-test-cases.json` | 跨平台共享的测试用例 |

## 核心原则

1. **normalizedURL 影响去重** — 同一内容的不同分享链接必须归一到相同的 normalizedURL
2. **platformContentID 影响导入判断** — 必须精确提取，跨平台保持一致
3. **正则表达式跨语言可复用** — 当前所有正则均为标准正则，C# 端可直接使用
4. **自定义 scheme 只含 ID** — 例如 `douyin://video/{id}`，不包含 tracking 参数

## Windows 端实现要求

- 必须通过 `url-normalizer-test-cases.json` 中的所有测试用例
- normalizedURL 输出必须与 macOS 端完全一致
- platformContentID 输出必须与 macOS 端完全一致
- 不得修改已有平台的识别规则
- 新增平台需在本文档中记录

## 已知限制

- `t.cn` 短链不支持
- 短链展开属于 Parser 层，不属于 URLNormalizer
- 当前未显式清理 tracking 参数（`utm_*`、`spm` 等）
