FROM python:3.11-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir --default-timeout=3000 --retries=20 -r requirements.txt

COPY app ./app

EXPOSE 8001

CMD ["python", "app/main.py"]
