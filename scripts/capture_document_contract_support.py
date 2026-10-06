"""Load the hyphenated capture CLI without executing its main entrypoint."""
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location('contract_capture', Path(__file__).with_name('capture-document-contract-proof.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
ASSEMBLY = module.ASSEMBLY
SOURCE_HASHES = module.SOURCE_HASHES
sha = module.sha
