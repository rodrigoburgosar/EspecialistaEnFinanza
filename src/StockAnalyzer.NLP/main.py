"""
Microservicio FastAPI para análisis de sentimiento financiero con FinBERT.
Expone endpoints REST que son consumidos por el backend C# del sistema.
"""

from contextlib import asynccontextmanager
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

from agente_sentimiento import cargar_modelo, analizar_sentimiento


# ── Modelos de request / response ──────────────────────────────────────────

class SolicitudSentimiento(BaseModel):
    """Cuerpo del request para análisis de sentimiento."""
    titulares: list[str]


class RespuestaSentimiento(BaseModel):
    """Respuesta del análisis de sentimiento."""
    puntaje: float
    total_analizados: int


# ── Ciclo de vida del servicio ──────────────────────────────────────────────

@asynccontextmanager
async def ciclo_de_vida(app: FastAPI):
    """
    Gestiona el ciclo de vida del servicio: carga el modelo al iniciar
    y libera recursos al detener.
    """
    print("Cargando modelo FinBERT (ProsusAI/finbert)...")
    cargar_modelo()
    print("Modelo cargado. Servicio listo.")
    yield
    print("Servicio detenido.")


# ── Aplicación FastAPI ──────────────────────────────────────────────────────

app = FastAPI(
    title="StockAnalyzer NLP Service",
    description="Microservicio de análisis de sentimiento financiero usando FinBERT.",
    version="1.0.0",
    lifespan=ciclo_de_vida
)


# ── Endpoints ───────────────────────────────────────────────────────────────

@app.get("/health", summary="Verificación de salud del servicio")
def verificar_salud() -> dict:
    """
    Retorna el estado del servicio para health checks desde el backend C#.

    Returns:
        Diccionario con el estado actual del servicio.
    """
    return {"estado": "ok", "servicio": "StockAnalyzer NLP"}


@app.post(
    "/sentiment",
    response_model=RespuestaSentimiento,
    summary="Analizar sentimiento de titulares financieros"
)
def analizar(solicitud: SolicitudSentimiento) -> RespuestaSentimiento:
    """
    Analiza el sentimiento financiero de una lista de titulares usando FinBERT.

    Args:
        solicitud: Objeto con la lista de titulares a analizar.

    Returns:
        Puntaje consolidado entre -1.0 (muy negativo) y +1.0 (muy positivo)
        y la cantidad de titulares procesados.
    """
    try:
        resultado = analizar_sentimiento(solicitud.titulares)
        return RespuestaSentimiento(**resultado)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error en análisis de sentimiento: {str(e)}")
