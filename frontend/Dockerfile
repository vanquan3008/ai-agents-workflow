# syntax=docker/dockerfile:1.7
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY . .
# Biến VITE_* được nhúng lúc build (không chứa secret). Mặc định đọc .env.production.
ARG VITE_AUTH_MODE
ARG VITE_DEV_USER_ID
ARG VITE_API_BASE_URL
ENV VITE_AUTH_MODE=${VITE_AUTH_MODE} \
    VITE_DEV_USER_ID=${VITE_DEV_USER_ID} \
    VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --retries=3 CMD wget -qO- http://127.0.0.1:8080/healthz || exit 1
