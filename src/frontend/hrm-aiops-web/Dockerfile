FROM node:22-alpine AS build
WORKDIR /app
COPY src/frontend/hrm-aiops-web/package.json src/frontend/hrm-aiops-web/package-lock.json* ./
RUN npm install
COPY src/frontend/hrm-aiops-web/ .
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
