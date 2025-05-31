from fastapi import FastAPI
from pydantic import BaseModel
import train_model

app = FastAPI()

class DateRange(BaseModel):
    start_date: str
    end_date: str

class UserInfo(BaseModel):
    name: str
    age: int

@app.post("/train_model/")
def train_and_predict(data: DateRange):
    # 수입/지출 예측 실행
    predicted_income, next_month_start_income, next_month_end_income = train_model.predict_income(data.start_date, data.end_date)
    predicted_expenditure, next_month_start_expenditure, next_month_end_expenditure = train_model.predict_expenditure(data.start_date, data.end_date)
    
    return {
        "predicted_income": predicted_income,
        "predicted_expenditure": predicted_expenditure,
        "next_month_start_income": next_month_start_income,
        "next_month_end_income": next_month_end_income,
        "next_month_start_expenditure": next_month_start_expenditure,
        "next_month_end_expenditure": next_month_end_expenditure
    }
@app.post("/hello/")
def hello():
    return { "hi" }

@app.post("/hellowithparameter/")
def hellowithparameter(user: UserInfo):
    return {
        "message": f"Hello, {user.name}. You are {user.age} years old."
    }