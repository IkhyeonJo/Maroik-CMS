import psycopg2
import os

# 환경변수로부터 DB 설정을 가져옵니다
DB_HOST = os.getenv("DBHOST", "postgres")
DB_NAME = os.getenv("DBNAME", "Maroik")
DB_USER = os.getenv("DBUSER", "postgres")
DB_PASSWORD = os.getenv("DBPASSWORD", "")

# PostgreSQL과 연결
def get_data_from_db(query):
    connection = psycopg2.connect(
        host=DB_HOST,
        user=DB_USER,
        password=DB_PASSWORD,
        database=DB_NAME
    )
    cursor = connection.cursor(cursor_factory=psycopg2.extras.DictCursor)
    cursor.execute(query)
    result = cursor.fetchall()
    cursor.close()
    connection.close()
    return result