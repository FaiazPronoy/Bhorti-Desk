# Build
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# Serve. This image runs nginx as a non-root user and listens on 8080.
FROM nginxinc/nginx-unprivileged:1.31-alpine
# The image fills in ${API_BASE_URL} in every file under /etc/nginx/templates at start
# and writes the result to /etc/nginx/conf.d.
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
ENV API_BASE_URL=http://localhost:8080
EXPOSE 8080
