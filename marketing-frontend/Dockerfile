FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
COPY tsconfig*.json vite.config.ts index.html ./
RUN npm install
COPY src ./src
RUN npm run build

FROM nginxinc/nginx-unprivileged:stable-alpine
ENV API_UPSTREAM=http://api:8080
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
