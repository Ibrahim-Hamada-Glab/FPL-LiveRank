# Stage 1 — build the Angular app with the production configuration.
FROM node:22-alpine AS build
WORKDIR /src

COPY client/package.json client/package-lock.json ./
RUN npm ci

COPY client/ ./
RUN npm run build -- --configuration production

# Stage 2 — serve via nginx and reverse-proxy /api to the backend container.
FROM nginx:1.27-alpine AS runtime
COPY client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/client/browser/ /usr/share/nginx/html/

EXPOSE 8081
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget -qO- http://127.0.0.1:8081/ >/dev/null || exit 1
