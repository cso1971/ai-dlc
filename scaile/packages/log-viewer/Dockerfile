FROM node:22-alpine AS build

ARG VITE_WEBHOOK_URL=http://localhost:8000

WORKDIR /app

COPY package.json ./
RUN corepack enable && corepack prepare pnpm@latest --activate \
    && pnpm install --no-frozen-lockfile

COPY . .

ENV VITE_WEBHOOK_URL=$VITE_WEBHOOK_URL
RUN pnpm build

# ── serve ──
FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 3000
CMD ["nginx", "-g", "daemon off;"]
