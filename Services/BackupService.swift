import Foundation
import OSLog
import SQLite3

/// 备份服务 - 支持导出和导入数据
final class BackupService: @unchecked Sendable {
    static let shared = BackupService()
    
    private let logger = Logger(subsystem: "com.archiver.app", category: "Backup")
    private let fileManager = FileManager.default
    
    private init() {}
    
    // MARK: - 备份
    
    /// 备份所有数据到用户选择的目录
    /// 返回导出的 zip 文件路径
    func backup(to destinationURL: URL) async throws -> URL {
        let dbPath = DataDirectory.database
        let mediaDir = DataDirectory.media
        let platformLogosDir = DataDirectory.platformLogos
        
        // 创建临时工作目录
        let tempDir = fileManager.temporaryDirectory.appendingPathComponent("archiver_backup_\(UUID().uuidString)")
        try fileManager.createDirectory(at: tempDir, withIntermediateDirectories: true)
        
        defer {
            try? fileManager.removeItem(at: tempDir)
        }
        
        // 1. 复制数据库
        if fileManager.fileExists(atPath: dbPath.path) {
            let dbBackup = tempDir.appendingPathComponent("archiver.db")
            try fileManager.copyItem(at: dbPath, to: dbBackup)
            logger.info("数据库已复制")
        }
        
        // 2. 复制媒体文件
        if fileManager.fileExists(atPath: mediaDir.path) {
            let mediaBackup = tempDir.appendingPathComponent("media")
            try fileManager.copyItem(at: mediaDir, to: mediaBackup)
            logger.info("媒体文件已复制")
        }
        
        // 3. 复制平台 Logo
        if fileManager.fileExists(atPath: platformLogosDir.path) {
            let logosBackup = tempDir.appendingPathComponent("platform_logos")
            try fileManager.copyItem(at: platformLogosDir, to: logosBackup)
            logger.info("平台 Logo 已复制")
        }
        
        // 4. 写入备份元信息
        let metadata: [String: Any] = [
            "version": Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0.0",
            "backupDate": ISO8601DateFormatter().string(from: Date()),
            "hasDatabase": fileManager.fileExists(atPath: dbPath.path),
            "hasMedia": fileManager.fileExists(atPath: mediaDir.path),
            "hasLogos": fileManager.fileExists(atPath: platformLogosDir.path)
        ]
        let metadataData = try JSONSerialization.data(withJSONObject: metadata)
        try metadataData.write(to: tempDir.appendingPathComponent("backup_info.json"))
        
        // 5. 打包为 zip
        let zipPath = destinationURL.appendingPathComponent("Archiver备份_\(formatDateForFilename(Date())).zip")
        try await createZip(from: tempDir, to: zipPath)
        
        logger.info("备份完成: \(zipPath.path, privacy: .public)")
        return zipPath
    }
    
    // MARK: - 还原
    
    /// 从备份 zip 还原数据（合并模式：不删除原有数据，在原有基础上新增）
    func restore(from backupURL: URL) async throws {
        
        // 1. 解压 zip 到临时目录
        let tempDir = fileManager.temporaryDirectory.appendingPathComponent("archiver_restore_\(UUID().uuidString)")
        try fileManager.createDirectory(at: tempDir, withIntermediateDirectories: true)
        
        defer {
            try? fileManager.removeItem(at: tempDir)
        }
        
        try await extractZip(from: backupURL, to: tempDir)
        
        // 2. 验证备份内容
        let dbFile = tempDir.appendingPathComponent("archiver.db")
        guard fileManager.fileExists(atPath: dbFile.path) else {
            throw BackupError.invalidBackup("备份中缺少数据库文件")
        }
        
        // 3. 合并数据库（使用 ATTACH + INSERT OR IGNORE）
        let currentDB = DataDirectory.database
        let backupDBPath = dbFile.path
        let currentDBPath = currentDB.path
        
        try await mergeDatabase(backupPath: backupDBPath, currentPath: currentDBPath)
        
        // 4. 合并媒体文件（只复制不存在的文件）
        let mediaBackup = tempDir.appendingPathComponent("media")
        if fileManager.fileExists(atPath: mediaBackup.path) {
            let currentMedia = DataDirectory.media
            try copyFilesIfNotExists(from: mediaBackup, to: currentMedia)
        }
        
        // 5. 合并平台 Logo（只复制不存在的文件）
        let logosBackup = tempDir.appendingPathComponent("platform_logos")
        if fileManager.fileExists(atPath: logosBackup.path) {
            let currentLogos = DataDirectory.platformLogos
            try copyFilesIfNotExists(from: logosBackup, to: currentLogos)
        }
        
        logger.info("还原完成")
    }
    
    /// 获取备份中的元信息
    func readBackupMetadata(from backupURL: URL) async -> BackupMetadata? {
        let tempDir = fileManager.temporaryDirectory.appendingPathComponent("archiver_meta_\(UUID().uuidString)")
        defer { try? fileManager.removeItem(at: tempDir) }
        
        do {
            try fileManager.createDirectory(at: tempDir, withIntermediateDirectories: true)
            try await extractZip(from: backupURL, to: tempDir)
            
            let metaFile = tempDir.appendingPathComponent("backup_info.json")
            let data = try Data(contentsOf: metaFile)
            let json = try JSONSerialization.jsonObject(with: data) as? [String: Any]
            
            return BackupMetadata(
                version: json?["version"] as? String ?? "未知",
                backupDate: json?["backupDate"] as? String ?? "未知",
                hasDatabase: json?["hasDatabase"] as? Bool ?? false,
                hasMedia: json?["hasMedia"] as? Bool ?? false,
                hasLogos: json?["hasLogos"] as? Bool ?? false
            )
        } catch {
            logger.error("读取备份信息失败: \(error.localizedDescription, privacy: .public)")
            return nil
        }
    }
    
    // MARK: - 私有方法
    
    private func createZip(from sourceDir: URL, to destination: URL) async throws {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/ditto")
        process.arguments = ["-c", "-k", "--sequesterRsrc", "--keepParent", sourceDir.path, destination.path]
        process.standardOutput = nil
        process.standardError = nil
        try process.run()
        process.waitUntilExit()
        
        guard process.terminationStatus == 0 else {
            throw BackupError.zipFailed("ditto 返回错误码 \(process.terminationStatus)")
        }
    }
    
    private func extractZip(from zipURL: URL, to destination: URL) async throws {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/ditto")
        process.arguments = ["-x", "-k", zipURL.path, destination.path]
        process.standardOutput = nil
        process.standardError = nil
        try process.run()
        process.waitUntilExit()
        
        guard process.terminationStatus == 0 else {
            throw BackupError.zipFailed("解压失败，错误码 \(process.terminationStatus)")
        }
    }
    
    private func mergeDatabase(backupPath: String, currentPath: String) throws {
        // 打开当前数据库，附加备份数据库，合并数据
        var db: OpaquePointer?
        guard sqlite3_open(currentPath, &db) == SQLITE_OK else {
            throw BackupError.invalidBackup("无法打开当前数据库")
        }
        defer { sqlite3_close(db) }
        
        // 附加备份数据库
        let attachSQL = "ATTACH DATABASE '\(backupPath)' AS backup_db"
        guard sqlite3_exec(db, attachSQL, nil, nil, nil) == SQLITE_OK else {
            // 如果附加失败，可能是因为表已存在，尝试直接复制
            try? fileManager.removeItem(atPath: currentPath)
            try fileManager.copyItem(atPath: backupPath, toPath: currentPath)
            return
        }
        defer { sqlite3_exec(db, "DETACH DATABASE backup_db", nil, nil, nil) }
        
        // 获取备份数据库中的所有表名
        var tableQuery: OpaquePointer?
        let tablesSQL = "SELECT name FROM backup_db.sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"
        guard sqlite3_prepare_v2(db, tablesSQL, -1, &tableQuery, nil) == SQLITE_OK else {
            try? fileManager.removeItem(atPath: currentPath)
            try fileManager.copyItem(atPath: backupPath, toPath: currentPath)
            return
        }
        defer { sqlite3_finalize(tableQuery) }
        
        var tables: [String] = []
        while sqlite3_step(tableQuery) == SQLITE_ROW {
            if let cString = sqlite3_column_text(tableQuery, 0) {
                tables.append(String(cString: cString))
            }
        }
        
        // 对每个表执行 INSERT OR IGNORE
        for table in tables {
            let insertSQL = "INSERT OR IGNORE INTO \(table) SELECT * FROM backup_db.\(table)"
            sqlite3_exec(db, insertSQL, nil, nil, nil)
        }
        
        // VACUUM 优化数据库
        sqlite3_exec(db, "VACUUM", nil, nil, nil)
        
        logger.info("数据库合并完成，合并了 \(tables.count) 个表")
    }
    
    private func copyFilesIfNotExists(from source: URL, to destination: URL) throws {
        guard fileManager.fileExists(atPath: source.path) else { return }
        
        // 创建目标目录（如果不存在）
        if !fileManager.fileExists(atPath: destination.path) {
            try fileManager.createDirectory(at: destination, withIntermediateDirectories: true)
        }
        
        // 遍历源目录中的文件
        let contents = try fileManager.contentsOfDirectory(at: source, includingPropertiesForKeys: nil)
        for item in contents {
            let fileName = item.lastPathComponent
            let destFile = destination.appendingPathComponent(fileName)
            
            if !fileManager.fileExists(atPath: destFile.path) {
                // 文件不存在，复制过去
                if item.hasDirectoryPath {
                    try copyFilesIfNotExists(from: item, to: destFile)
                } else {
                    try fileManager.copyItem(at: item, to: destFile)
                }
            }
        }
    }
    
    private func formatDateForFilename(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyyMMdd_HHmm"
        return formatter.string(from: date)
    }
}

// MARK: - 类型定义

enum BackupError: LocalizedError {
    case invalidBackup(String)
    case zipFailed(String)
    
    var errorDescription: String? {
        switch self {
        case .invalidBackup(let reason): return "备份文件无效: \(reason)"
        case .zipFailed(let reason): return "压缩/解压失败: \(reason)"
        }
    }
}

struct BackupMetadata {
    let version: String
    let backupDate: String
    let hasDatabase: Bool
    let hasMedia: Bool
    let hasLogos: Bool
}
