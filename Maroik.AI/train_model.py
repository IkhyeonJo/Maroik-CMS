import pandas as pd
from db import get_data_from_db
from sklearn.model_selection import train_test_split
from sklearn.linear_model import LinearRegression
from sklearn.metrics import mean_squared_error
from datetime import datetime, timedelta

# 수입 예측 함수
def predict_income(start_date, end_date):
    # DB에서 수입 데이터를 가져오기
    query = f"SELECT * FROM \"Income\" WHERE \"Created\" BETWEEN '{start_date}' AND '{end_date}';"
    data = get_data_from_db(query)
    
    # 데이터가 없으면 빈 DataFrame 반환
    if not data:
        return None, None, None
    
    df = pd.DataFrame(data)

    # 데이터 전처리
    df['Amount'] = df['Amount'].astype(float)
    df['Created'] = pd.to_datetime(df['Created'])

    # 예시 모델: 선형 회귀
    X = df[['Amount']]  # 예시로 금액만 사용
    y = df['Amount']  # 예시로 금액을 예측 대상으로 설정

    # 학습용/검증용 데이터 분할
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

    # 모델 훈련
    model = LinearRegression()
    model.fit(X_train, y_train)

    # 예측 및 평가
    predictions = model.predict(X_test)
    mse = mean_squared_error(y_test, predictions)
    print(f"Income Model Mean Squared Error: {mse}")

    # 익월 예측: 현재 날짜를 기준으로 익월로 예측
    current_date = datetime.now()
    next_month = current_date.replace(day=1) + timedelta(days=32)  # 익월 1일
    next_month_start = next_month.replace(day=1).strftime('%Y-%m-%d')
    next_month_end = (next_month.replace(day=28) + timedelta(days=4)).replace(day=1).strftime('%Y-%m-%d')  # 익월 말일

    # 예측된 수입 반환
    predicted_income = model.predict([[1000]])  # 예시: 1000의 금액에 대한 예측
    return predicted_income.tolist(), next_month_start, next_month_end

# 지출 예측 함수
def predict_expenditure(start_date, end_date):
    # DB에서 지출 데이터를 가져오기
    query = f"SELECT * FROM \"Expenditure\" WHERE \"Created\" BETWEEN '{start_date}' AND '{end_date}';"
    data = get_data_from_db(query)
    
    # 데이터가 없으면 빈 DataFrame 반환
    if not data:
        return None, None, None
    
    df = pd.DataFrame(data)

    # 데이터 전처리
    df['Amount'] = df['Amount'].astype(float)
    df['Created'] = pd.to_datetime(df['Created'])

    # 예시 모델: 선형 회귀
    X = df[['Amount']]  # 예시로 금액만 사용
    y = df['Amount']  # 예시로 금액을 예측 대상으로 설정

    # 학습용/검증용 데이터 분할
    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

    # 모델 훈련
    model = LinearRegression()
    model.fit(X_train, y_train)

    # 예측 및 평가
    predictions = model.predict(X_test)
    mse = mean_squared_error(y_test, predictions)
    print(f"Expenditure Model Mean Squared Error: {mse}")

    # 익월 예측: 현재 날짜를 기준으로 익월로 예측
    current_date = datetime.now()
    next_month = current_date.replace(day=1) + timedelta(days=32)  # 익월 1일
    next_month_start = next_month.replace(day=1).strftime('%Y-%m-%d')
    next_month_end = (next_month.replace(day=28) + timedelta(days=4)).replace(day=1).strftime('%Y-%m-%d')  # 익월 말일

    # 예측된 지출 반환
    predicted_expenditure = model.predict([[500]])  # 예시: 500의 금액에 대한 예측
    return predicted_expenditure.tolist(), next_month_start, next_month_end
