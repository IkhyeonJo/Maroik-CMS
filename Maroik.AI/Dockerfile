FROM python:3.9-slim

RUN apt-get update && apt-get install -y libpq-dev libicu-dev && apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY ["Maroik.AI/requirements.txt", "Maroik.AI/"]
RUN pip install --no-cache-dir -r /app/Maroik.AI/requirements.txt

COPY ./Maroik.AI/ /app/

EXPOSE 8000

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
