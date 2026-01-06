docker build -t echophase .
docker tag echophase localhost:5000/echophase:latest
docker push localhost:5000/echophase:latest
cd compose
docker compose down  
docker compose pull
docker compose up -d
