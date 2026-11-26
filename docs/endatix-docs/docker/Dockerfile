# The built docs (build/, from `pnpm run build`) behind nginx, as a non-root user on port 8080.
# Writes only to /tmp, so it runs with a read-only root filesystem and an emptyDir at /tmp.
FROM docker.io/nginxinc/nginx-unprivileged:1.30.6-alpine-slim@sha256:5d8bb9437d74a600ec5dbf90c035451c724dc33eac8d3a71078f6f8612c345f6

COPY docker/nginx.conf /etc/nginx/nginx.conf
COPY docker/site-headers.conf /etc/nginx/site-headers.conf
COPY build/ /usr/share/nginx/html/

EXPOSE 8080
USER 101
CMD ["nginx", "-g", "daemon off;"]
