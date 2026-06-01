import SwiftUI

// MARK: - ImageCache

class ImageCache: ObservableObject {
    private var cache: [String: NSImage] = [:]
    
    func get(_ url: String) -> NSImage? {
        cache[url]
    }
    
    func set(_ url: String, image: NSImage) {
        cache[url] = image
    }
    
    func allImages() -> [NSImage] {
        Array(cache.values)
    }
    
    func imageURLs() -> [String] {
        Array(cache.keys)
    }
}

// MARK: - MarkdownView

struct MarkdownView: View {
    let text: String
    var imageCache = ImageCache()
    var localFileMap: [String: URL] = [:]
    var onImageTap: ((Int) -> Void)? = nil
    
    var body: some View {
        let blocks = parseBlocks()
        return VStack(alignment: .leading, spacing: 12) {
            let imageIndices = blocks.enumerated().filter { $0.element.isImage }.map { $0.offset }
            ForEach(Array(blocks.enumerated()), id: \.offset) { index, block in
                switch block {
                case .text(let content):
                    ParagraphText(content: content)
                case .image(let url, let alt):
                    AsyncImageView(url: url, alt: alt, imageCache: imageCache, localFileMap: localFileMap)
                        .onTapGesture {
                            if let imageIdx = imageIndices.firstIndex(of: index) {
                                onImageTap?(imageIdx)
                            }
                        }
                }
            }
        }
    }
    
    private enum Block {
        case text(String)
        case image(url: String, alt: String)
        
        var isImage: Bool {
            if case .image = self { return true }
            return false
        }
    }
    
    func imageURLs() -> [String] {
        let blocks = parseBlocks()
        return blocks.compactMap { block in
            if case .image(let url, _) = block { return url }
            return nil
        }
    }
    
    func cachedImages() -> [NSImage] {
        imageURLs().compactMap { imageCache.get($0) }
    }
    
    private func parseBlocks() -> [Block] {
        if !text.contains("![") {
            let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
            return trimmed.isEmpty ? [] : [.text(trimmed)]
        }
        
        var blocks: [Block] = []
        let lines = text.components(separatedBy: "\n")
        var currentText = ""
        
        let imagePattern = #"!\[([^\]]*)\]\(([^)]*)\)"#
        guard let imageRegex = try? NSRegularExpression(pattern: imagePattern) else {
            return [.text(text)]
        }
        
        for line in lines {
            let nsLine = line as NSString
            let matches = imageRegex.matches(in: line, range: NSRange(location: 0, length: nsLine.length))
            
            if matches.isEmpty {
                if !currentText.isEmpty { currentText += "\n" }
                currentText += line
            } else {
                if !currentText.isEmpty {
                    blocks.append(.text(currentText.trimmingCharacters(in: .whitespacesAndNewlines)))
                    currentText = ""
                }
                var lastEnd = 0
                for match in matches {
                    let before = nsLine.substring(to: match.range.location)
                    if !before.isEmpty {
                        blocks.append(.text(before.trimmingCharacters(in: .whitespacesAndNewlines)))
                    }
                    let alt = nsLine.substring(with: match.range(at: 1))
                    let url = nsLine.substring(with: match.range(at: 2))
                    blocks.append(.image(url: url, alt: alt))
                    lastEnd = match.range.location + match.range.length
                }
                let remaining = nsLine.substring(from: lastEnd)
                if !remaining.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    currentText = remaining
                }
            }
        }
        
        if !currentText.isEmpty {
            let trimmed = currentText.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty {
                blocks.append(.text(trimmed))
            }
        }
        
        return blocks.isEmpty ? [.text(text)] : blocks
    }
}

// MARK: - ParagraphText

struct ParagraphText: View {
    let content: String
    
    private static let maxChunkSize = 500
    
    var body: some View {
        let chunks = smartSplit(content)
        VStack(alignment: .leading, spacing: 8) {
            ForEach(Array(chunks.enumerated()), id: \.offset) { _, chunk in
                if let attributedString = try? AttributedString(
                    markdown: chunk,
                    options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace)
                ) {
                    Text(attributedString)
                        .textSelection(.enabled)
                } else {
                    Text(chunk)
                        .textSelection(.enabled)
                }
            }
        }
    }
    
    private func smartSplit(_ text: String) -> [String] {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.count <= Self.maxChunkSize {
            return [trimmed]
        }
        
        let paragraphs = trimmed.components(separatedBy: "\n\n")
        var result: [String] = []
        
        for para in paragraphs {
            if para.count <= Self.maxChunkSize {
                result.append(para)
            } else {
                result.append(contentsOf: splitBySentences(para))
            }
        }
        
        return result.isEmpty ? [trimmed] : result
    }
    
    private func splitBySentences(_ text: String) -> [String] {
        let pattern = "(?<=[。！？.!?])\\s*"
        guard let regex = try? NSRegularExpression(pattern: pattern) else {
            return splitByLength(text)
        }
        
        let nsText = text as NSString
        let range = NSRange(location: 0, length: nsText.length)
        let splits = regex.matches(in: text, range: range).map { $0.range.location }
        
        guard !splits.isEmpty else {
            return splitByLength(text)
        }
        
        var sentences: [String] = []
        var lastEnd = 0
        for splitPos in splits {
            let sentence = nsText.substring(with: NSRange(location: lastEnd, length: splitPos - lastEnd))
                .trimmingCharacters(in: .whitespacesAndNewlines)
            if !sentence.isEmpty {
                sentences.append(sentence)
            }
            lastEnd = splitPos
        }
        let remaining = nsText.substring(from: lastEnd)
            .trimmingCharacters(in: .whitespacesAndNewlines)
        if !remaining.isEmpty {
            sentences.append(remaining)
        }
        
        return mergeSmallChunks(sentences)
    }
    
    private func splitByLength(_ text: String) -> [String] {
        var result: [String] = []
        var start = text.startIndex
        while start < text.endIndex {
            let end = text.index(start, offsetBy: Self.maxChunkSize, limitedBy: text.endIndex) ?? text.endIndex
            result.append(String(text[start..<end]))
            start = end
        }
        return result
    }
    
    private func mergeSmallChunks(_ chunks: [String]) -> [String] {
        var result: [String] = []
        var buffer = ""
        
        for chunk in chunks {
            if buffer.isEmpty {
                buffer = chunk
            } else if buffer.count + chunk.count + 1 <= Self.maxChunkSize {
                buffer += "\n" + chunk
            } else {
                result.append(buffer)
                buffer = chunk
            }
        }
        if !buffer.isEmpty {
            result.append(buffer)
        }
        
        return result
    }
}

// MARK: - AsyncImageView

struct AsyncImageView: View {
    let url: String
    let alt: String
    var imageCache: ImageCache? = nil
    var localFileMap: [String: URL] = [:]
    
    @State private var image: NSImage?
    @State private var isLoading = true
    
    var body: some View {
        Group {
            if let image = image {
                Image(nsImage: image)
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(maxHeight: 300)
                    .clipShape(RoundedRectangle(cornerRadius: 8))
                    .contextMenu {
                        Button {
                            let pasteboard = NSPasteboard.general
                            pasteboard.clearContents()
                            pasteboard.writeObjects([image])
                        } label: {
                            Label("复制", systemImage: "doc.on.doc")
                        }
                        if let imageURL = URL(string: url) {
                            Button {
                                let panel = NSSavePanel()
                                panel.nameFieldStringValue = imageURL.lastPathComponent
                                panel.allowedContentTypes = [.image]
                                if panel.runModal() == .OK, let dest = panel.url {
                                    try? Data(contentsOf: imageURL).write(to: dest)
                                }
                            } label: {
                                Label("另存为", systemImage: "square.and.arrow.down")
                            }
                        }
                    }
            } else if isLoading {
                ProgressView()
                    .frame(height: 100)
            } else {
                HStack {
                    Image(systemName: "photo")
                        .foregroundStyle(.tertiary)
                    Text(alt.isEmpty ? "图片加载失败" : alt)
                        .font(.caption)
                        .foregroundStyle(.tertiary)
                }
                .frame(maxWidth: .infinity, minHeight: 80)
                .background(.quaternary)
                .clipShape(RoundedRectangle(cornerRadius: 8))
            }
        }
        .contentShape(Rectangle())
        .onAppear { loadImage() }
    }
    
    private func loadImage() {
        if let cached = imageCache?.get(url) {
            self.image = cached
            self.isLoading = false
            return
        }
        
        if let localURL = localFileMap[url], let nsImage = NSImage(contentsOf: localURL) {
            self.image = nsImage
            self.isLoading = false
            imageCache?.set(url, image: nsImage)
            return
        }
        
        guard let imageURL = URL(string: url) else {
            isLoading = false
            return
        }
        Task {
            let session = URLSession.shared
            var request = URLRequest(url: imageURL)
            request.setValue("Mozilla/5.0", forHTTPHeaderField: "User-Agent")
            guard let (data, _) = try? await session.data(for: request),
                  let nsImage = NSImage(data: data) else {
                isLoading = false
                return
            }
            self.image = nsImage
            self.isLoading = false
            imageCache?.set(url, image: nsImage)
        }
    }
}
