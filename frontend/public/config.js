// Cấu hình runtime của dashboard. Khi chạy trong container, file này được thay bằng bản sinh từ biến môi trường
// (xem docker/40-devagent-config.sh). Khi chạy `npm run dev`, để trống để dùng biến VITE_*.
window.__DEVAGENT_CONFIG__ = window.__DEVAGENT_CONFIG__ || {};
