import SwiftUI

extension Platform {
    var iconName: String {
        switch self {
        case .douyin: return "music.note"
        case .xiaohongshu: return "book.fill"
        case .coolapk: return "apps.iphone"
        case .bilibili: return "play.tv"
        case .github: return "chevron.left.forwardslash.chevron.right"
        case .youtube: return "play.rectangle.fill"
        case .x: return "bubble.left.and.bubble.right.fill"
        case .weibo: return "bubble.left.and.bubble.right.fill"
        case .zhihu: return "text.bubble.fill"
        case .douban: return "book.closed.fill"
        case .custom: return "star.fill"
        }
    }
    
    var brandColor: Color {
        switch self {
        case .douyin: return .black
        case .xiaohongshu: return .red
        case .coolapk: return .green
        case .bilibili: return .cyan
        case .github: return .primary
        case .youtube: return .red
        case .x: return .black
        case .weibo: return Color(red: 255/255, green: 96/255, blue: 0/255)
        case .zhihu: return Color(red: 0/255, green: 102/255, blue: 255/255)
        case .douban: return Color(red: 0/255, green: 150/255, blue: 0/255)
        case .custom: return .purple
        }
    }
}
