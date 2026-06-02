FROM node:22-alpine
RUN apk add --no-cache chromium xvfb ffmpeg pulseaudio pulseaudio-utils font-noto font-noto-cjk
WORKDIR /app
COPY package.json ./
COPY src ./src
EXPOSE 8787
CMD ["npm", "start"]
