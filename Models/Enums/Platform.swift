import Foundation

/// 平台枚举
enum Platform: String, Codable, CaseIterable, Identifiable {
    case douyin
    case xiaohongshu
    case coolapk
    case bilibili
    case github
    case youtube
    case x
    case weibo
    case zhihu
    case douban
    case custom
    
    var id: String { rawValue }
    
    var defaultDisplayName: String {
        switch self {
        case .douyin: return "抖音"
        case .xiaohongshu: return "小红书"
        case .coolapk: return "酷安"
        case .bilibili: return "B站"
        case .github: return "GitHub"
        case .youtube: return "YouTube"
        case .x: return "X"
        case .weibo: return "微博"
        case .zhihu: return "知乎"
        case .douban: return "豆瓣"
        case .custom: return "自定义"
        }
    }
    
    var displayName: String {
        PlatformCustomization.displayName(for: self)
    }
}
