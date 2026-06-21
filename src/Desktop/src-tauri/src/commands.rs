use tauri::command;

#[command]
pub async fn open_file_dialog() -> Result<Option<String>, String> {
    // Uses native file dialog via Tauri's dialog API
    // This is called from the frontend when running in desktop mode
    Ok(None)
}

#[command]
pub fn get_app_version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}
