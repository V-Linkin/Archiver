import SwiftUI
import AVFoundation
import AppKit

/// 视频播放器视图 - 用 AVPlayerLayer 实现，完全控制事件处理
struct VideoPlayerView: NSViewRepresentable {
    let url: URL
    
    func makeNSView(context: Context) -> VideoPlayerNSView {
        let view = VideoPlayerNSView()
        view.loadVideo(url: url)
        return view
    }
    
    func updateNSView(_ nsView: VideoPlayerNSView, context: Context) {}
}

/// 底层 NSView - 使用 AVPlayerLayer，忽略滚轮事件
class VideoPlayerNSView: NSView {
    private var player: AVPlayer?
    private var playerLayer: AVPlayerLayer?
    private var hasPlayed = false
    
    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
    }
    
    required init?(coder: NSCoder) {
        super.init(coder: coder)
        wantsLayer = true
    }
    
    func loadVideo(url: URL) {
        let player = AVPlayer(url: url)
        self.player = player
        
        let playerLayer = AVPlayerLayer(player: player)
        playerLayer.videoGravity = .resizeAspect
        playerLayer.backgroundColor = NSColor.black.cgColor
        playerLayer.frame = bounds
        self.layer?.addSublayer(playerLayer)
        self.playerLayer = playerLayer
    }
    
    override func layout() {
        super.layout()
        playerLayer?.frame = bounds
    }
    
    /// 忽略滚轮事件 - 不调用 super，事件传递给外层 ScrollView
    override func scrollWheel(with event: NSEvent) {
        self.nextResponder?.scrollWheel(with: event)
    }
    
    /// 鼠标点击时播放/暂停
    override func mouseDown(with event: NSEvent) {
        if let player = player {
            if hasPlayed {
                if player.timeControlStatus == .playing {
                    player.pause()
                } else {
                    player.play()
                }
            } else {
                player.seek(to: .zero)
                player.play()
                hasPlayed = true
            }
        }
    }
    
    deinit {
        player?.pause()
    }
}
