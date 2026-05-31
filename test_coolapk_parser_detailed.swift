import Foundation

// 模拟 CoolapkParser 的行为
print("测试 CoolapkParser 逻辑")
print(String(repeating: "=", count: 60))

// 模拟 WebView 返回的数据
let mockWebViewResult = "COOLAPK_JSON:{\"title\":\"测试标题\",\"text\":\"测试正文内容超过20个字符\",\"author\":\"测试作者\",\"images\":[\"https://example.com/image1.jpg\",\"https://example.com/image2.jpg\"],\"cover\":\"https://example.com/image1.jpg\",\"debug\":\"bodyLen:500,title:yes,author:yes,text:30,images:2\"}"

print("模拟 WebView 返回结果:")
print(mockWebViewResult)
print()

// 模拟 CoolapkParser.parseViaWebView 的逻辑
if mockWebViewResult.hasPrefix("COOLAPK_JSON:") {
    print("✓ 检测到 COOLAPK_JSON 前缀")
    
    let jsonStr = String(mockWebViewResult.dropFirst("COOLAPK_JSON:".count))
    print("JSON 字符串: \(jsonStr)")
    print()
    
    if let jsonData = jsonStr.data(using: .utf8),
       let json = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any] {
        print("✓ JSON 解析成功")
        
        let title = json["title"] as? String
        let author = json["author"] as? String
        let text = json["text"] as? String
        let images = json["images"] as? [String] ?? []
        let cover = json["cover"] as? String
        
        print("标题: \(title ?? "无")")
        print("作者: \(author ?? "无")")
        print("正文: \(text ?? "无")")
        print("图片数量: \(images.count)")
        print("封面: \(cover ?? "无")")
        print()
        
        // 检查条件判断
        if title != nil || text != nil || !images.isEmpty {
            print("✓ 满足内容条件")
            
            // 创建 ParsedContent
            print("创建 ParsedContent 对象:")
            print("  title: \(title ?? "nil")")
            print("  body: \(text ?? "nil")")
            print("  author: \(author ?? "nil")")
            print("  coverURL: \(cover ?? "nil")")
            print("  imageURLs: \(images)")
            print("  platformContentID: (extracted)")
        } else {
            print("✗ 不满足内容条件")
        }
    } else {
        print("✗ JSON 解析失败")
    }
} else {
    print("✗ 没有检测到 COOLAPK_JSON 前缀")
    print("实际返回: \(mockWebViewResult.prefix(100))...")
}

print()
print(String(repeating: "=", count: 60))
print("逻辑测试完成")
