use tauri::command;
use tauri_plugin_dialog::DialogExt;
use tauri_plugin_dialog::FilePath;

#[command]
pub async fn open_file_dialog(app: tauri::AppHandle) -> Result<Option<String>, String> {
    // Uses native file dialog via the Tauri dialog plugin.
    // Runs on a blocking thread since the picker blocks until the user responds.
    let file_path = tauri::async_runtime::spawn_blocking(move || {
        app.dialog()
            .file()
            .add_filter("Video", &["mp4", "mkv", "avi", "mov", "webm", "m4v", "wmv", "flv"])
            .blocking_pick_file()
    })
    .await
    .map_err(|e| e.to_string())?;

    let normalized = match file_path {
        Some(FilePath::Path(path)) => Some(path.to_string_lossy().to_string()),
        Some(FilePath::Url(url)) => {
            let path = url
                .to_file_path()
                .map_err(|_| "Selected file URL cannot be converted to a local path.".to_string())?;
            Some(path.to_string_lossy().to_string())
        }
        None => None,
    };

    Ok(normalized)
}

#[command]
pub fn get_app_version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}
