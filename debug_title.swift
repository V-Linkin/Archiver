import Foundation
import WebKit

// 调试标题提取
final class TitleDebugLoader: NSObject, WKNavigationDelegate {
    private var webView: WKWebView?
    private var continuation: CheckedContinuation<String?, Never>?
    
    @MainActor
    func loadFullContent(from url: URL) async -> String? {
        let config = WKWebViewConfiguration()
        config.suppressesIncrementalRendering = true
        
        let webView = WKWebView(frame: .zero, configuration: config)
        webView.navigationDelegate = self
        self.webView = webView
        
        return await withCheckedContinuation { continuation in
            self.continuation = continuation
            webView.load(URLRequest(url: url))
            
            Task {
                try? await Task.sleep(for: .seconds(15))
                if self.continuation != nil {
                    self.finishWith(nil)
                }
            }
        }
    }
    
    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        Task { @MainActor in
            let js = """
            (function() {
                var debugInfo = {
                    titleSelectors: {},
                    contentSelectors: {},
                    foundTitle: ''
                };
                
                // 测试各种标题选择器
                var titleSelectors = [
                    '.detail-title',
                    '.feed-title',
                    '.post-title',
                    'h1.title',
                    '[class*="title"]',
                    'title'
                ];
                
                for (var i = 0; i < titleSelectors.length; i++) {
                    var selector = titleSelectors[i];
                    var el = document.querySelector(selector);
                    if (el) {
                        debugInfo.titleSelectors[selector] = {
                            exists: true,
                            innerText: el.innerText ? el.innerText.substring(0, 100) : 'null',
                            className: el.className || 'no-class'
                        };
                        
                        if (el.innerText && el.innerText.trim().length > 0) {
                            var titleText = el.innerText.trim();
                            if (titleText !== '酷安APP' && titleText.length > 5) {
                                debugInfo.foundTitle = titleText;
                                break;
                            }
                        }
                    } else {
                        debugInfo.titleSelectors[selector] = { exists: false };
                    }
                }
                
                // 检查内容区域
                var contentSelectors = [
                    '.detail-content',
                    '.feed-content',
                    'article',
                    '[class*="content"]'
                ];
                
                for (var i = 0; i < contentSelectors.length; i++) {
                    var selector = contentSelectors[i];
                    var el = document.querySelector(selector);
                    if (el) {
                        debugInfo.contentSelectors[selector] = {
                            exists: true,
                            textLength: el.innerText ? el.innerText.length : 0,
                            firstLine: el.innerText ? el.innerText.split('\\n')[0] : ''
                        };
                    } else {
                        debugInfo.contentSelectors[selector] = { exists: false };
                    }
                }
                
                return 'DEBUG_TITLES:' + JSON.stringify(debugInfo);
            })()
            """
            
            do {
                if let result = try await webView.evaluateJavaScript(js) as? String, !result.isEmpty {
                    self.finishWith(result)
                } else {
                    self.finishWith(nil)
                }
            } catch {
                self.finishWith(nil)
            }
        }
    }
    
    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        Task { @MainActor in
            self.finishWith(nil)
        }
    }
    
    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        decisionHandler(.allow)
    }
    
    @MainActor
    private func finishWith(_ result: String?) {
        continuation?.resume(returning: result)
        continuation = nil
        webView?.navigationDelegate = nil
        webView = nil
    }
}

@MainActor
func debugTitles() async {
    let loader = TitleDebugLoader()
    let testURL = URL(string: "https://www.coolapk.com/feed/72069721?s=M2I2ZDE5NTIxYzA3MTgyZzZhMWMyYzIwega1620")!
    
    print("调试标题提取")
    print("测试链接: \(testURL.absoluteString)")
    print()
    
    if let result = await loader.loadFullContent(from: testURL) {
        if result.hasPrefix("DEBUG_TITLES:") {
            let jsonStr = String(result.dropFirst("DEBUG_TITLES:".count))
            if let jsonData = jsonStr.data(using: .utf8),
               let json = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any] {
                
                print("=== 标题选择器调试 ===")
                if let titleSelectors = json["titleSelectors"] as? [String: Any] {
                    for (selector, info) in titleSelectors {
                        if let infoDict = info as? [String: Any] {
                            if let exists = infoDict["exists"] as? Bool, exists {
                                print("✓ \(selector):")
                                if let innerText = infoDict["innerText"] as? String {
                                    print("  内容: \(innerText)")
                                }
                                if let className = infoDict["className"] as? String {
                                    print("  类名: \(className)")
                                }
                            } else {
                                print("✗ \(selector): 不存在")
                            }
                        }
                    }
                }
                
                print()
                print("=== 内容选择器调试 ===")
                if let contentSelectors = json["contentSelectors"] as? [String: Any] {
                    for (selector, info) in contentSelectors {
                        if let infoDict = info as? [String: Any] {
                            if let exists = infoDict["exists"] as? Bool, exists {
                                print("✓ \(selector):")
                                if let textLength = infoDict["textLength"] as? Int {
                                    print("  文本长度: \(textLength)")
                                }
                                if let firstLine = infoDict["firstLine"] as? String {
                                    print("  第一行: \(firstLine)")
                                }
                            } else {
                                print("✗ \(selector): 不存在")
                            }
                        }
                    }
                }
                
                print()
                print("=== 提取结果 ===")
                if let foundTitle = json["foundTitle"] as? String {
                    print("找到的标题: \(foundTitle)")
                } else {
                    print("未找到合适的标题")
                }
            }
        }
    }
}

// 运行调试
if #available(macOS 14.0, *) {
    Task { @MainActor in
        await debugTitles()
        exit(0)
    }
    
    RunLoop.main.run()
} else {
    print("需要 macOS 14.0+")
    exit(1)
}
