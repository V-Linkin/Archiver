import Foundation
import GRDB
import os.log

final class DatabaseManager: Sendable {
    static let shared = DatabaseManager()
    
    let db: DatabaseQueue
    
    private let logger = Logger(subsystem: "com.archiver.app", category: "Database")
    
    private init() {
        _ = DataDirectory.base
        
        let dbPath = DataDirectory.database.path
        logger.info("数据库路径: \(dbPath)")
        
        do {
            db = try DatabaseQueue(path: dbPath)
            logger.info("数据库队列创建成功")
        } catch {
            logger.error("数据库队列创建失败: \(error.localizedDescription)")
            fatalError("数据库初始化失败: \(error)")
        }
        
        do {
            try Self.migrate(db)
            logger.info("数据库迁移成功")
        } catch {
            logger.error("数据库迁移失败: \(error.localizedDescription)")
            fatalError("数据库迁移失败: \(error)")
        }
    }
    
    // MARK: - SQL File Loading
    
    private static func loadSQL(named name: String) throws -> String {
        guard let url = Bundle.main.url(forResource: name, withExtension: "sql", subdirectory: "db/migrations") else {
            fatalError("无法找到 SQL 文件: \(name).sql (db/migrations/)")
        }
        do {
            let content = try String(contentsOf: url, encoding: .utf8)
            return content
        } catch {
            fatalError("读取 SQL 文件失败: \(name).sql — \(error.localizedDescription)")
        }
    }
    
    // MARK: - Migration
    
    private static func migrate(_ db: DatabaseQueue) throws {
        var migrator = DatabaseMigrator()
        
        migrator.registerMigration("v1_createTables") { db in
            let sql = try loadSQL(named: "v1_create_tables")
            try db.execute(sql: sql)
        }
        
        migrator.registerMigration("v2_fts") { db in
            let sql = try loadSQL(named: "v2_fts")
            try db.execute(sql: sql)
        }
        
        // v3 和 v4 已经在 v1 中包含，跳过
        // 使用 migrator.registerMigrationWithDeferredMigration 会导致问题
        // 所以我们用 try? 来执行 ALTER TABLE（如果已存在会失败但不影响）
        
        try migrator.migrate(db)
        
        // 确保 custom_platform_id 列存在
        try? db.write { db in
            try db.execute(sql: "ALTER TABLE items ADD COLUMN custom_platform_id TEXT")
        }
        try? db.write { db in
            try db.execute(sql: "ALTER TABLE folders ADD COLUMN custom_platform_id TEXT")
        }
    }
}
