import Foundation
import WebKit

// 调试版本 - 详细输出提取过程
final class DebugWebLoader: NSObject, WKNavigationDelegate {
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
                    var bodyLen = document.body ? document.body.innerText.length : 0;
                    var debugInfo = {
                        url: url,
                        bodyLen: bodyLen,
                        hasInitialData: !!document.getElementById('js-initialData'),
                        hasCoolapkScript: false,
                        coolapkData: null
                    };
                    
                    // === 酷安处理 ===
                    if (url.indexOf('coolapk.com') !== -1 || url.indexOf('coolapk1s.com') !== -1) {
                        debugInfo.hasCoolapkScript = true;
                        
                        var coolapkResult = {
                            title: '',
                            text: '',
                            author: '',
                            images: [],
                            cover: '',
                            debug: {}
                        };
                        
                        // 1. 检查是否有 SSR 数据
                        var scripts = document.getElementsByTagName('script');
                        for (var i = 0; i < scripts.length; i++) {
                            if (scripts[i].textContent.indexOf('window.__INITIAL_STATE__') !== -1) {
                                debugInfo.hasSSRData = true;
                                break;
                            }
                        }
                        
                        // 2. 尝试提取文章标题
                        var titleSelectors = [
                            '.detail-title',
                            '.feed-title',
                            '.post-title',
                            'h1.title',
                            '[class*="title"]',
                            'title'
                        ];
                        
                        for (var i = 0; i < titleSelectors.length; i++) {
                            var titleEl = document.querySelector(titleSelectors[i]);
                            if (titleEl) {
                                debugInfo['titleSelector_' + i] = titleEl.innerText ? titleEl.innerText.substring(0, 50) : 'null';
                                if (titleEl.innerText && titleEl.innerText.trim().length > 0) {
                                    var titleText = titleEl.innerText.trim();
                                    if (titleText !== '酷安APP' && titleText.length > 5) {
                                        coolapkResult.title = titleText;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // 3. 尝试提取正文内容
                        var contentSelectors = [
                            '.detail-content',
                            '.feed-content',
                            'article',
                            '[class*="content"]',
                            '.post-content'
                        ];
                        
                        for (var i = 0; i < contentSelectors.length; i++) {
                            var contentEl = document.querySelector(contentSelectors[i]);
                            if (contentEl) {
                                debugInfo['contentSelector_' + i] = contentEl.innerText ? contentEl.innerText.substring(0, 100) : 'null';
                                if (contentEl.innerText && contentEl.innerText.trim().length > 20) {
                                    coolapkResult.text = contentEl.innerText.trim();
                                    break;
                                }
                            }
                        }
                        
                        // 4. 尝试提取图片
                        var imgSelectors = [
                            '.detail-content img',
                            '.feed-content img',
                            'article img',
                            '[class*="content"] img',
                            '.post-content img'
                        ];
                        
                        var allImages = [];
                        for (var i = 0; i < imgSelectors.length; i++) {
                            var imgEls = document.querySelectorAll(imgSelectors[i]);
                            debugInfo['imgSelector_' + i + '_count'] = imgEls.length;
                            if (imgEls.length > 0) {
                                for (var j = 0; j < imgEls.length; j++) {
                                    if (imgEls[j].src && imgEls[j].src.indexOf('http') === 0) {
                                        allImages.push(imgEls[j].src);
                                    }
                                }
                            }
                        }
                        
                        // 过滤非内容图片
                        coolapkResult.images = allImages.filter(function(src) {
                            return !src.includes('static.coolapk.com/static/web') &&
                                   !src.includes('avatar.coolapk.com') &&
                                   !src.includes('emoticons') &&
                                   !src.includes('product_logo') &&
                                   !src.includes('beian.png') &&
                                   !src.includes('qr/image');
                        });
                        
                        // 5. 尝试提取作者
                        var authorSelectors = [
                            '.user-name',
                            '.author-name',
                            '.feed-user',
                            '[class*="user"]',
                            '[class*="author"]'
                        ];
                        
                        for (var i = 0; i < authorSelectors.length; i++) {
                            var authorEl = document.querySelector(authorSelectors[i]);
                            if (authorEl) {
                                debugInfo['authorSelector_' + i] = authorEl.innerText ? authorEl.innerText.substring(0, 50) : 'null';
                                if (authorEl.innerText) {
                                    var authorText = authorEl.innerText.trim();
                                    authorText = authorText.replace(/\\n/g, ' ').replace(/\\s+/g, ' ');
                                    if (authorText.length > 0) {
                                        coolapkResult.author = authorText;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // 6. 设置封面
                        coolapkResult.cover = coolapkResult.images.length > 0 ? coolapkResult.images[0] : '';
                        
                        // 7. 更新调试信息
                        coolapkResult.debug = {
                            bodyLen: bodyLen,
                            title: coolapkResult.title ? 'yes' : 'no',
                            author: coolapkResult.author ? 'yes' : 'no',
                            textLen: coolapkResult.text.length,
                            imageCount: coolapkResult.images.length,
                            allImageCount: allImages.length
                        };
                        
                        debugInfo.coolapkData = coolapkResult;
                        
                        if (coolapkResult.text.length > 20 || coolapkResult.images.length > 0) {
                            return 'DEBUG_COOLAPK_JSON:' + JSON.stringify(debugInfo);
                        } else {
                            return 'DEBUG_COOLAPK_EMPTY:' + JSON.stringify(debugInfo);
                        }
                    }
                    
                    return 'DEBUG_UNKNOWN:' + JSON.stringify(debugInfo);
                } catch(e) {
                    return 'DEBUG_ERROR:' + e.toString();
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
func debugCoolapkLoader() async {
    let loader = DebugWebLoader()
    let testURL = URL(string: "https://www.coolapk.com/feed/72069721?s=M2I2ZDE5NTIxYzA3MTgyZzZhMWMyYzIwega1620")!
    
    print("开始调试酷安链接: \(testURL.absoluteString)")
    
    if let result = await loader.loadFullContent(from: testURL) {
        print("加载成功:")
        
        if result.hasPrefix("DEBUG_COOLAPK_JSON:") || result.hasPrefix("DEBUG_COOLAPK_EMPTY:") {
            let jsonStr = String(result.dropFirst(result.hasPrefix("DEBUG_COOLAPK_JSON:") ? "DEBUG_COOLAPK_JSON:".count : "DEBUG_COOLAPK_EMPTY:".count))
            if let jsonData = jsonStr.data(using: .utf8),
               let json = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any] {
                
                print("\n=== 调试信息 ===")
                print("URL: \(json["url"] ?? "")")
                print("正文长度: \(json["bodyLen"] ?? 0)")
                print("是否有SSR数据: \(json["hasSSRData"] ?? false)")
                
                if let coolapkData = json["coolapkData"] as? [String: Any] {
                    print("\n=== 酷安数据 ===")
                    print("标题: \(coolapkData["title"] ?? "无")")
                    print("作者: \(coolapkData["author"] ?? "无")")
                    print("正文长度: \((coolapkData["text"] as? String ?? "").count) 字符")
                    print("图片数量: \((coolapkData["images"] as? [String] ?? []).count)")
                    print("封面: \(coolapkData["cover"] ?? "无")")
                    
                    if let debug = coolapkData["debug"] as? [String: Any] {
                        print("\n=== 详细调试 ===")
                        print("标题提取: \(debug["title"] ?? "无")")
                        print("作者提取: \(debug["author"] ?? "无")")
                        print("正文长度: \(debug["textLen"] ?? 0)")
                        print("图片数量: \(debug["imageCount"] ?? 0)")
                        print("所有图片数: \(debug["allImageCount"] ?? 0)")
                    }
                }
                
                // 显示选择器调试信息
                print("\n=== 选择器调试 ===")
                for (key, value) in json {
                    if key.hasPrefix("titleSelector_") || key.hasPrefix("contentSelector_") || 
                       key.hasPrefix("imgSelector_") || key.hasPrefix("authorSelector_") {
                        print("\(key): \(value)")
                    }
                }
            }
        } else {
            print("未识别的响应格式: \(result)")
        }
    } else {
        print("加载失败或超时")
    }
}

// 运行调试
if #available(macOS 14.0, *) {
    Task { @MainActor in
        await debugCoolapkLoader()
        exit(0)
    }
    
    RunLoop.main.run()
} else {
    print("需要 macOS 14.0+")
    exit(1)
}
