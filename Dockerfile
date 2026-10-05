FROM nginx:alpine

# Remove default nginx site and replace with Render-compatible port 10000 configuration
RUN rm -f /etc/nginx/conf.d/default.conf
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy distribution landing page and packaged Windows binary downloads
COPY index.html /usr/share/nginx/html/index.html
COPY downloads/ /usr/share/nginx/html/downloads/

# Render routes incoming web traffic to port 10000
EXPOSE 10000

CMD ["nginx", "-g", "daemon off;"]
