from fastapi import FastAPI
from pydantic import BaseModel
import os
import train_model
from transformers import AutoTokenizer, AutoModelForCausalLM
import torch

app = FastAPI()

# Load local LLM model using environment variable
model_path = os.environ.get("LOCAL_LLM_MODEL_PATH", "./models")
tokenizer = AutoTokenizer.from_pretrained(model_path)
model = AutoModelForCausalLM.from_pretrained(model_path)

class DateRange(BaseModel):
    start_date: str
    end_date: str

class UserInfo(BaseModel):
    name: str
    age: int

class LLMRequest(BaseModel):
    prompt: str
    max_tokens: int = 50

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

@app.post("/llm/generate")
def generate_text(request: LLMRequest):
    inputs = tokenizer(request.prompt, return_tensors="pt")
    with torch.no_grad():
        outputs = model.generate(**inputs, max_new_tokens=request.max_tokens)
    text = tokenizer.decode(outputs[0], skip_special_tokens=True)
    return {"generated_text": text}
