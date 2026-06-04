-- ============================================================
-- 拾屿 Archiver — FTS5 全文搜索契约
-- 来源: Database/DatabaseManager.swift v2_fts migration
--       Database/SearchRepository.swift
-- ============================================================

-- 创建 FTS5 虚拟表（来自 v2_fts migration）
CREATE VIRTUAL TABLE IF NOT EXISTS items_fts USING fts5(
    title,
    body,
    tokenize='unicode61'
);

-- FTS5 索引重建命令（来自 SearchRepository.rebuildIndex）
INSERT INTO items_fts(items_fts) VALUES('rebuild');

-- ============================================================
-- FTS5 使用说明（来自 SearchRepository.swift）
-- ============================================================
--
-- 搜索查询:
--   SELECT items.*, rank
--   FROM items_fts
--   JOIN items ON items.id = items_fts.rowid
--   WHERE items_fts MATCH ?
--   AND items.deleted_at IS NULL
--   ORDER BY rank
--
-- 更新索引:
--   INSERT INTO items_fts (rowid, title, body) VALUES (?, ?, ?)
--
-- 删除索引:
--   DELETE FROM items_fts WHERE rowid=?
--
-- 重建索引:
--   INSERT INTO items_fts(items_fts) VALUES('rebuild')
