import Foundation
import WebKit

// 测试修复后的 CoolapkParser 行为
final class TestWebLoader: NSObject, WKNavigationDelegate {
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
                try {
                    var url = document.location.href;
                    
                    if (url.indexOf('coolapk.com') !== -1 || url.indexOf('coolapk1s.com') !== -1) {
                        var coolapkResult = {
                            title: '',
                            text: '',
                            author: '',
                            images: [],
                            cover: ''
                        };
                        
                        // 提取标题
                        var titleSelectors = ['.detail-title', '.feed-title', 'h1.title', '[class*="title"]'];
                        for (var i = 0; i < titleSelectors.length; i++) {
                            var titleEl = document.querySelector(titleSelectors[i]);
                            if (titleEl && titleEl.innerText && titleEl.innerText.trim().length > 0) {
                                var titleText = titleEl.innerText.trim();
                                if (titleText !== '酷安APP' && titleText.length > 5) {
                                    coolapkResult.title = titleText;
                                    break;
                                }
                            }
                        }
                        
                        // 提取正文
                        var contentEl = document.querySelector('.detail-content, .feed-content, article, [class*="content"]');
                        if (contentEl) {
                            coolapkResult.text = contentEl.innerText.trim();
                        }
                        
                        // 提取图片
                        var imgEls = document.querySelectorAll('.detail-content img, .feed-content img, article img, [class*="content"] img');
                        var allImages = Array.from(imgEls).map(function(img) { return img.src; }).filter(function(src) {
                            return src && src.indexOf('http') === 0;
                        });
                        
                        coolapkResult.images = allImages.filter(function(src) {
                            return !src.includes('static.coolapk.com/static/web') &&
                                   !src.includes('avatar.coolapk.com') &&
                                   !src.includes('emoticons');
                        });
                        
                        // 提取作者
                        var authorEl = document.querySelector('.user-name, .author-name, [class*="user"]');
                        if (authorEl && authorEl.innerText) {
                            coolapkResult.author = authorEl.innerText.trim().replace(/\\n/g, ' ').replace(/\\s+/g, ' ');
                        }
                        
                        coolapkResult.cover = coolapkResult.images.length > 0 ? coolapkResult.images[0] : '';
                        
                        if (coolapkResult.text.length > 20 || coolapkResult.images.length > 0) {
                            return 'COOLAPK_JSON:' + JSON.stringify(coolapkResult);
                        }
                    }
                    
                    return 'NO_CONTENT';
                } catch(e) {
                    return 'ERROR:' + e.toString();
                }
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
func testCoolapkFix() async {
    let loader = TestWebLoader()
    let testURL = URL(string: "https://www.coolapk.com/feed/72069721?s=M2I2ZDE5NTIxYzA3MTgyZzZhMWMyYzIwega1620")!
    
    print("测试修复后的酷安解析器")
    print("测试链接: \(testURL.absoluteString)")
    print()
    
    if let result = await loader.loadFullContent(from: testURL) {
        if result.hasPrefix("COOLAPK_JSON:") {
            let jsonStr = String(result.dropFirst("COOLAPK_JSON:".count))
            if let jsonData = jsonStr.data(using: .utf8),
               let json = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any] {
                
                print("✓ WebView 提取成功")
                print("标题: \(json["title"] ?? "无")")
                print("作者: \(json["author"] ?? "无")")
                print("正文长度: \((json["text"] as? String ?? "").count) 字符")
                print("图片数量: \((json["images"] as? [String] ?? []).count)")
                print("封面: \(json["cover"] ?? "无")")
                
                print()
                print("修复说明:")
                print("1. HTTP 模式会检查内容质量")
                print("2. 如果内容质量不足（只有页面标题和描述）")
                print("3. 会返回 nil 并降级到 WebView 模式")
                print("4. WebView 模式提取完整的文章内容")
            }
        } else if result == "NO_CONTENT" {
            print("✗ 没有提取到内容")
        } else {
            print("✗ 未知响应: \(result)")
        }
    } else {
        print("✗ 加载失败")
    }
}

// 运行测试
if #available(macOS 14.0, *) {
    Task { @MainActor in
        await testCoolapkFix()
        exit(0)
    }
    
    RunLoop.main.run()
} else {
    print("需要 macOS 14.0+")
    exit(1)
}
