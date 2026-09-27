# Suitability

Find for you. Style for you. Scenic match for you.

AI-powered fit shopping with on-device camera try-on. Describe an event, get body/style/setting scores, preview garments on your laptop or phone camera, compare options, and check out — no AR/VR headset.

## Run locally

Start the API:

```sh
cd backend
python3.12 -m venv .venv   # or any Python 3.11+
source .venv/bin/activate
python -m pip install -r requirements.txt
cp .env.example .env   # optional: add OPENAI_API_KEY / REPLICATE_API_TOKEN
uvicorn app.main:app --reload
```

Start the web app:

```sh
cd frontend
npm install
npm run dev
```

Open the Vite URL (typically `http://127.0.0.1:5173`). API docs: `http://127.0.0.1:8000/docs`.

### Routes

| Path | Page |
|------|------|
| `/` | Landing |
| `/event` | Event & fit profile (+ voice) |
| `/browse` | Recommendations + agent swarm search |
| `/try-on` | Camera generative try-on |
| `/compare` | Side-by-side compare |
| `/assistant` | Chat assistant (+ voice) |
| `/bag` | Bag & checkout |

### Optional env (backend/.env)

- `OPENAI_API_KEY` — Whisper transcription + gpt-image try-on
- `REPLICATE_API_TOKEN` — IDM-VTON-style try-on
- `OLLAMA_HOST` / `OLLAMA_MODEL` — optional swarm re-rank

Without keys: browser Transformers.js Whisper (or Web Speech fallback), and image-based torso garment composite for try-on.

## Demo flow

1. `/event` — describe an event (or use the mic), set measurements and style.
2. `/browse` — ranked products; run the agent swarm on a free-text ask.
3. `/try-on` — start camera, then **Generate try-on** for photo-style garment replacement.
4. Compare, ask the assistant, add to bag, checkout.

## Camera notes

Camera access works on `localhost` or HTTPS. Pose guide stays on-device. Generative try-on sends one captured frame to the API (OpenAI/Replicate when keyed; otherwise local composite on the API host).

## Checks

```sh
cd backend && .venv/bin/python -m unittest discover -s tests
cd frontend && npm run lint && npm run build
```
