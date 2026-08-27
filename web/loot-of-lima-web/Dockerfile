# The web client: built once, then served as static files by nginx.
#
# Deliberately not `ng serve` in a container. The point of this image is to have the app running
# without a dev server; when you want the dev server, you stop this container and run `npm start`,
# which listens on the same port so nothing else has to change.

FROM node:24-alpine AS build
WORKDIR /app

# npm ci before the source, so editing a component does not reinstall node_modules. ci rather than
# install because the lockfile is committed and a build that quietly resolved different versions
# would not be a build of what is in git.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY nginx-proxy.conf /etc/nginx/snippets/loot-proxy.conf

# The application builder emits the browser bundle under dist/<project>/browser.
COPY --from=build /app/dist/loot-of-lima-web/browser /usr/share/nginx/html

EXPOSE 4200
