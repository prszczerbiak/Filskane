from pydantic import BaseModel, Field


class ThresholdRange(BaseModel):
    min: float = 0.2
    max: float = 0.6


class Thresholds(BaseModel):
    ndvi: ThresholdRange = Field(default_factory=ThresholdRange)
    gndvi: ThresholdRange = Field(default_factory=ThresholdRange)
    ndwi: ThresholdRange = Field(default_factory=ThresholdRange)


class DbscanRequest(BaseModel):
    points: list[list[float]]
    ndvi_values: list[float]
    eps: float = 5
    min_samples: int = 3
    ellipse_h: int = 3
    ellipse_w: int = 4


class DbscanResponse(BaseModel):
    cluster_ids: list[int]
    ndvi_means: dict[str, float]


class MultiIndexGroupingRequest(BaseModel):
    ndvi: list[list[float]]
    gndvi: list[list[float]]
    ndwi: list[list[float]]
    field_points: list[list[float]] = Field(default_factory=list)
    thresholds: Thresholds = Field(default_factory=Thresholds)
    eps: float = 5
    min_samples: int = 3
    ellipse_h: int = 3
    ellipse_w: int = 4


class MultiIndexGroupingResponse(BaseModel):
    combined_classes: list[list[int]]
    cluster_ids: list[int]
    cluster_means: dict[str, float]
    cluster_points: list[list[int]]
