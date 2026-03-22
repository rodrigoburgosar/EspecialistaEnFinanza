"""
Agente de análisis de sentimiento financiero usando el modelo ProsusAI/finbert.
Procesa titulares de noticias y retorna un score consolidado entre -1.0 y +1.0.
"""

from transformers import pipeline, Pipeline


# Modelo cargado una sola vez al iniciar el servicio (vía lifespan en main.py)
_modelo: Pipeline | None = None


def cargar_modelo() -> None:
    """
    Carga el modelo FinBERT en memoria.
    Debe invocarse una sola vez durante el inicio del servicio.
    """
    global _modelo
    _modelo = pipeline(
        task="text-classification",
        model="ProsusAI/finbert",
        top_k=None
    )


def analizar_sentimiento(titulares: list[str]) -> dict:
    """
    Analiza el sentimiento financiero de una lista de titulares usando FinBERT.

    Args:
        titulares: Lista de titulares de noticias financieras en inglés.

    Returns:
        Diccionario con 'puntaje' (float entre -1.0 y +1.0) y 'total_analizados' (int).
    """
    if not titulares:
        return {"puntaje": 0.0, "total_analizados": 0}

    if _modelo is None:
        raise RuntimeError("El modelo FinBERT no ha sido cargado. Llama a cargar_modelo() primero.")

    resultados = _modelo(titulares, truncation=True, max_length=512)
    scores = []

    for resultado in resultados:
        # resultado es una lista de dicts: [{"label": "positive", "score": 0.9}, ...]
        etiquetas = {item["label"]: item["score"] for item in resultado}
        score_positivo = etiquetas.get("positive", 0.0)
        score_negativo = etiquetas.get("negative", 0.0)
        # Score neto: positivo - negativo (neutro contribuye con 0)
        scores.append(score_positivo - score_negativo)

    puntaje_consolidado = sum(scores) / len(scores)

    return {
        "puntaje": round(puntaje_consolidado, 4),
        "total_analizados": len(titulares)
    }
