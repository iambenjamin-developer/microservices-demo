# syntax=docker/dockerfile:1

# The React app is static files: Node builds them, nginx serves them. There is no Node process at
# runtime and no server-side rendering — the app talks to the Gateway from the browser.
# The build context is the repository root:
#   docker build -f src/Web/Dockerfile .

FROM node:24-alpine AS build
WORKDIR /app

COPY src/Web/package.json src/Web/package-lock.json ./
RUN npm ci

COPY src/Web/ ./

# Vite inlines VITE_* variables at build time, so the Gateway address is a build argument, not a
# container variable: this is the address the *browser* uses, not one inside the compose network.
ARG VITE_GATEWAY_URL=http://localhost:5100
ENV VITE_GATEWAY_URL=$VITE_GATEWAY_URL
RUN npm run build

FROM nginx:1.29-alpine AS final
COPY src/Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
