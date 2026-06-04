CREATE TABLE IF NOT EXISTS items (
    id TEXT PRIMARY KEY,
    title TEXT,
    body TEXT,
    original_url TEXT NOT NULL,
    platform TEXT NOT NULL,
    platform_content_id TEXT,
    normalized_url TEXT NOT NULL,
    author TEXT,
    author_id TEXT,
    publish_date REAL,
    import_date REAL NOT NULL,
    modify_date REAL NOT NULL,
    content_status TEXT NOT NULL DEFAULT 'normal',
    archive_status TEXT NOT NULL DEFAULT 'pending',
    media_status TEXT NOT NULL DEFAULT 'textOnly',
    cover_asset_id TEXT,
    folder_id TEXT,
    remark TEXT,
    is_starred INTEGER NOT NULL DEFAULT 0,
    version INTEGER NOT NULL DEFAULT 1,
    deleted_at REAL,
    custom_platform_id TEXT
);

CREATE INDEX IF NOT EXISTS idx_items_platform ON items(platform);
CREATE INDEX IF NOT EXISTS idx_items_archive_status ON items(archive_status);
CREATE INDEX IF NOT EXISTS idx_items_folder ON items(folder_id);
CREATE INDEX IF NOT EXISTS idx_items_normalized_url ON items(normalized_url);
CREATE INDEX IF NOT EXISTS idx_items_import_date ON items(import_date);
CREATE INDEX IF NOT EXISTS idx_items_deleted_at ON items(deleted_at);

CREATE TABLE IF NOT EXISTS media_assets (
    id TEXT PRIMARY KEY,
    item_id TEXT NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    type TEXT NOT NULL,
    local_path TEXT,
    remote_url TEXT,
    file_name TEXT NOT NULL,
    file_size INTEGER NOT NULL DEFAULT 0,
    mime_type TEXT,
    width INTEGER,
    height INTEGER,
    duration REAL,
    checksum TEXT,
    download_status TEXT NOT NULL DEFAULT 'pending',
    thumbnail_path TEXT,
    created_at REAL NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_media_item ON media_assets(item_id);
CREATE INDEX IF NOT EXISTS idx_media_type ON media_assets(type);

CREATE TABLE IF NOT EXISTS folders (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    parent_id TEXT REFERENCES folders(id) ON DELETE CASCADE,
    platform TEXT NOT NULL,
    created_at REAL NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    custom_platform_id TEXT
);

CREATE INDEX IF NOT EXISTS idx_folders_platform ON folders(platform);
CREATE INDEX IF NOT EXISTS idx_folders_parent ON folders(parent_id);

CREATE TABLE IF NOT EXISTS trash_records (
    id TEXT PRIMARY KEY,
    item_id TEXT NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    deleted_at REAL NOT NULL,
    auto_delete_at REAL NOT NULL,
    original_folder_id TEXT,
    original_archive_status TEXT NOT NULL,
    media_paths TEXT
);

CREATE INDEX IF NOT EXISTS idx_trash_deleted_at ON trash_records(deleted_at);

CREATE TABLE IF NOT EXISTS import_tasks (
    id TEXT PRIMARY KEY,
    original_url TEXT NOT NULL,
    normalized_url TEXT NOT NULL,
    platform TEXT,
    status TEXT NOT NULL DEFAULT 'pending',
    progress REAL NOT NULL DEFAULT 0,
    error_message TEXT,
    item_id TEXT REFERENCES items(id),
    created_at REAL NOT NULL,
    completed_at REAL,
    retry_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_tasks_status ON import_tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_created ON import_tasks(created_at);
