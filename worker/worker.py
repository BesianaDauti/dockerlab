from flask import Flask, request, jsonify
import subprocess, tempfile, os, shutil

app = Flask(__name__)

TIMEOUT_PYTHON = 10   
TIMEOUT_CSHARP = 30   
TEMPLATE_DIR   = "/app/csharp_template"

def run_python(code):
    with tempfile.NamedTemporaryFile(suffix=".py", delete=False, mode="w") as f:
        f.write(code)
        path = f.name
    try:
        r = subprocess.run(
            ["python3", path],
            capture_output=True, text=True, timeout=TIMEOUT_PYTHON
        )
        return {
            "stdout":    r.stdout,
            "stderr":    r.stderr,
            "exit_code": r.returncode
        }
    except subprocess.TimeoutExpired:
        return {"error": f"Timeout ({TIMEOUT_PYTHON}s) — loop infinit?", "timeout": True}
    finally:
        os.unlink(path)

def run_csharp(code):
    if "static void Main" not in code and "static async Task Main" not in code:
        code = f"""using System;
using System.Linq;
using System.Collections.Generic;

class Program {{
    static void Main(string[] args) {{
        {code}
    }}
}}"""

    tmpdir = tempfile.mkdtemp()
    try:
        shutil.copytree(TEMPLATE_DIR, tmpdir, dirs_exist_ok=True)

        with open(os.path.join(tmpdir, "Program.cs"), "w") as f:
            f.write(code)

        r = subprocess.run(
            ["dotnet", "run", "--project", tmpdir, "--no-restore"],
            capture_output=True, text=True, timeout=TIMEOUT_CSHARP
        )

        build_prefixes = ("Build succeeded", "Warning(s)", "Error(s)",
                          "Determining", "  Determining", "MSBuild")
        clean = "\n".join(
            l for l in r.stdout.split("\n")
            if l.strip() and not any(l.strip().startswith(p) for p in build_prefixes)
        )

        return {
            "stdout":    clean,
            "stderr":    r.stderr,
            "exit_code": r.returncode
        }

    except subprocess.TimeoutExpired:
        return {"error": f"Timeout ({TIMEOUT_CSHARP}s) — loop infinit?", "timeout": True}

    finally:
        shutil.rmtree(tmpdir, ignore_errors=True)

@app.route("/execute", methods=["POST"])
def execute():
    data     = request.get_json(silent=True) or {}
    language = data.get("language", "python").lower().strip()
    code     = data.get("code", "").strip()

    if not code:
        return jsonify({"error": "Kodi është bosh"}), 400

    try:
        if language == "python":
            result = run_python(code)
        elif language == "csharp":
            result = run_csharp(code)
        else:
            return jsonify({"error": f"Gjuhë e panjohur: {language}"}), 400

        return jsonify(result)

    except Exception as e:
        return jsonify({"error": f"Gabim i brendshëm: {str(e)}"}), 500

@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok"})

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=False)