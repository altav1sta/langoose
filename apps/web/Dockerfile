FROM node:22-alpine AS build
WORKDIR /app

COPY ["package.json", "package-lock.json", "./"]
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final

ENV LANGOOSE_API_BASE_URL=http://localhost:5000

COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
COPY --from=build /app/dist /usr/share/nginx/html

RUN chmod +x /docker-entrypoint.d/40-runtime-config.sh

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
