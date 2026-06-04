import Foundation

/// 搜索服务 — 封装 SearchRepository 调用，验证 View → Service → Repository 分层模式
final class SearchService: @unchecked Sendable {
    private let searchRepo: SearchRepository

    init(searchRepo: SearchRepository = SearchRepository()) {
        self.searchRepo = searchRepo
    }

    /// 搜索内容，返回匹配的结果列表
    /// - Parameter query: 搜索关键词
    /// - Returns: 匹配的 SearchResult 数组，空 query 返回空数组
    func search(query: String) throws -> [SearchResult] {
        try searchRepo.search(query: query)
    }
}
