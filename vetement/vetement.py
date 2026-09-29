#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""LibreVies - atelier de vetements.

Petit atelier autonome :
- importe une image et supprime son fond par flood-fill ;
- fabrique un vetement parametrique simple ;
- affiche un apercu 2D lisible ;
- exporte un GLB texturise et un fichier JSON de placement.

Le GLB est volontairement le format de sortie principal : son export est
realise ici sans Blender ni module FBX proprietaire. Le fichier JSON garde la
zone du corps couverte pour que le jeu puisse masquer la peau plus tard.
"""

from __future__ import annotations

import io
import json
import math
import os
import struct
import sys
import tkinter as tk
from dataclasses import asdict, dataclass, field
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from typing import Iterable, List, Optional, Sequence, Tuple

try:
    from PIL import Image, ImageDraw, ImageTk
except ImportError:
    Image = None
    ImageDraw = None
    ImageTk = None

Vec3 = Tuple[float, float, float]
Vec2 = Tuple[float, float]
Color = Tuple[float, float, float, float]


@dataclass
class GarmentProject:
    name: str = "vetement"
    template: str = "Image decoupee"
    image_path: str = ""
    anchor: str = "torse"
    coverage: List[str] = field(default_factory=lambda: ["torse_avant"])
    width: float = 0.40
    height: float = 0.30
    depth: float = 0.06
    scale: float = 1.0
    offset_x: float = 0.0
    offset_y: float = 0.0
    offset_z: float = 0.0
    rotation_y: float = 0.0
    background_tolerance: int = 28
    mode: str = "Image avec transparence"


class MeshData:
    """Mesh simple avec sommets deja dedoubles par face pour les normales."""

    def __init__(self) -> None:
        self.positions: List[Vec3] = []
        self.normals: List[Vec3] = []
        self.uvs: List[Vec2] = []
        self.indices: List[int] = []

    def face(self, points: Sequence[Vec3], normal: Vec3,
             uvs: Sequence[Vec2] = ((0.0, 0.0), (1.0, 0.0),
                                    (1.0, 1.0), (0.0, 1.0))) -> None:
        start = len(self.positions)
        self.positions.extend(points)
        self.normals.extend([normal] * 4)
        self.uvs.extend(uvs)
        self.indices.extend((start, start + 1, start + 2,
                             start, start + 2, start + 3))

    def box(self, center: Vec3, size: Vec3,
            uv: Sequence[Vec2] = ((0.0, 0.0), (1.0, 0.0),
                                  (1.0, 1.0), (0.0, 1.0))) -> None:
        cx, cy, cz = center
        sx, sy, sz = size[0] / 2.0, size[1] / 2.0, size[2] / 2.0
        self.face(((cx - sx, cy - sy, cz + sz), (cx + sx, cy - sy, cz + sz),
                   (cx + sx, cy + sy, cz + sz), (cx - sx, cy + sy, cz + sz)),
                  (0.0, 0.0, 1.0), uv)
        self.face(((cx + sx, cy - sy, cz - sz), (cx - sx, cy - sy, cz - sz),
                   (cx - sx, cy + sy, cz - sz), (cx + sx, cy + sy, cz - sz)),
                  (0.0, 0.0, -1.0), uv)
        self.face(((cx - sx, cy - sy, cz - sz), (cx - sx, cy - sy, cz + sz),
                   (cx - sx, cy + sy, cz + sz), (cx - sx, cy + sy, cz - sz)),
                  (-1.0, 0.0, 0.0), uv)
        self.face(((cx + sx, cy - sy, cz + sz), (cx + sx, cy - sy, cz - sz),
                   (cx + sx, cy + sy, cz - sz), (cx + sx, cy + sy, cz + sz)),
                  (1.0, 0.0, 0.0), uv)
        self.face(((cx - sx, cy + sy, cz + sz), (cx + sx, cy + sy, cz + sz),
                   (cx + sx, cy + sy, cz - sz), (cx - sx, cy + sy, cz - sz)),
                  (0.0, 1.0, 0.0), uv)
        self.face(((cx - sx, cy - sy, cz - sz), (cx + sx, cy - sy, cz - sz),
                   (cx + sx, cy - sy, cz + sz), (cx - sx, cy - sy, cz + sz)),
                  (0.0, -1.0, 0.0), uv)

    def dome(self, center_x: float, center_y: float, center_z: float,
             radius_x: float, radius_y: float, radius_z: float,
             segments: int = 24, rings: int = 5) -> None:
        """Dome aplati pour un bonnet de soutien-gorge."""
        start = len(self.positions)
        for ring in range(rings):
            t = ring / float(rings - 1)
            angle = t * math.pi * 0.5
            rr = math.sin(angle)
            dome_z = math.cos(angle) * radius_z
            for segment in range(segments):
                around = segment * math.pi * 2.0 / segments
                x = center_x + math.cos(around) * radius_x * rr
                y = center_y + math.sin(around) * radius_y * rr
                z = center_z + dome_z
                self.positions.append((x, y, z))
                self.normals.append((math.cos(around) * rr,
                                     math.sin(around) * rr,
                                     math.cos(angle)))
                self.uvs.append(((math.cos(around) + 1.0) * 0.5,
                                 (math.sin(around) + 1.0) * 0.5))
        for ring in range(rings - 1):
            for segment in range(segments):
                a = start + ring * segments + segment
                b = start + ring * segments + (segment + 1) % segments
                c = start + (ring + 1) * segments + (segment + 1) % segments
                d = start + (ring + 1) * segments + segment
                self.indices.extend((a, c, b, a, d, c))

    def quad(self, center: Vec3, size: Vec2, z: float) -> None:
        cx, cy = center[0], center[1]
        sx, sy = size[0] * 0.5, size[1] * 0.5
        self.face(((cx - sx, cy - sy, z), (cx + sx, cy - sy, z),
                   (cx + sx, cy + sy, z), (cx - sx, cy + sy, z)),
                  (0.0, 0.0, 1.0))


class GLBWriter:
    @staticmethod
    def _align(data: bytearray) -> None:
        while len(data) % 4:
            data.append(0)

    @staticmethod
    def _accessor(view: int, component_type: int, count: int,
                  kind: str, minimum=None, maximum=None) -> dict:
        result = {"bufferView": view, "componentType": component_type,
                  "count": count, "type": kind}
        if minimum is not None:
            result["min"] = minimum
        if maximum is not None:
            result["max"] = maximum
        return result

    @classmethod
    def write(cls, path: Path, mesh: MeshData, color: Color,
              texture_png: Optional[bytes] = None,
              translation: Vec3 = (0.0, 0.0, 0.0),
              scale: float = 1.0,
              rotation_y: float = 0.0) -> None:
        binary = bytearray()
        views = []
        accessors = []

        def add_blob(blob: bytes, target: Optional[int] = None) -> int:
            cls._align(binary)
            offset = len(binary)
            binary.extend(blob)
            view = {"buffer": 0, "byteOffset": offset,
                    "byteLength": len(blob)}
            if target is not None:
                view["target"] = target
            views.append(view)
            return len(views) - 1

        positions = b"".join(struct.pack("<3f", *v) for v in mesh.positions)
        normals = b"".join(struct.pack("<3f", *v) for v in mesh.normals)
        uvs = b"".join(struct.pack("<2f", *v) for v in mesh.uvs)
        indices = b"".join(struct.pack("<I", i) for i in mesh.indices)
        vp = add_blob(positions, 34962)
        vn = add_blob(normals, 34962)
        vu = add_blob(uvs, 34962)
        vi = add_blob(indices, 34963)

        if mesh.positions:
            mins = [min(v[i] for v in mesh.positions) for i in range(3)]
            maxs = [max(v[i] for v in mesh.positions) for i in range(3)]
        else:
            mins = [0.0, 0.0, 0.0]
            maxs = [0.0, 0.0, 0.0]
        accessors.append(cls._accessor(vp, 5126, len(mesh.positions), "VEC3", mins, maxs))
        accessors.append(cls._accessor(vn, 5126, len(mesh.normals), "VEC3"))
        accessors.append(cls._accessor(vu, 5126, len(mesh.uvs), "VEC2"))
        accessors.append(cls._accessor(vi, 5125, len(mesh.indices), "SCALAR"))

        image_json = []
        texture_json = []
        material = {"pbrMetallicRoughness": {
            "baseColorFactor": list(color),
            "metallicFactor": 0.0,
            "roughnessFactor": 0.78}}
        if texture_png:
            image_view = add_blob(texture_png)
            image_json = [{"bufferView": image_view, "mimeType": "image/png"}]
            texture_json = [{"source": 0}]
            material["pbrMetallicRoughness"]["baseColorTexture"] = {"index": 0}

        root = {
            "asset": {"version": "2.0", "generator": "LibreVies Atelier Vetement"},
            "scene": 0,
            "scenes": [{"nodes": [0]}],
            "nodes": [{"mesh": 0, "translation": list(translation),
                       "scale": [scale, scale, scale],
                       "rotation": [0.0, math.sin(rotation_y * math.pi / 360.0), 0.0,
                                    math.cos(rotation_y * math.pi / 360.0)]}],
            "meshes": [{"primitives": [{
                "attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2},
                "indices": 3, "material": 0}]}],
            "buffers": [{"byteLength": len(binary)}],
            "bufferViews": views,
            "accessors": accessors,
            "materials": [material],
            "images": image_json,
            "textures": texture_json,
        }
        json_bytes = json.dumps(root, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
        while len(json_bytes) % 4:
            json_bytes += b" "
        while len(binary) % 4:
            binary.append(0)
        # JSON precede le BIN dans un GLB.
        header = struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(json_bytes) + 8 + len(binary))
        output = bytearray(header)
        output.extend(struct.pack("<I4s", len(json_bytes), b"JSON"))
        output.extend(json_bytes)
        output.extend(struct.pack("<I4s", len(binary), b"BIN\x00"))
        output.extend(binary)
        path.write_bytes(output)


def remove_background(image: "Image.Image", tolerance: int) -> "Image.Image":
    """Rend transparent le fond connecte aux quatre bords de l'image."""
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    visited = bytearray(width * height)
    queue: List[Tuple[int, int]] = []
    for x in range(width):
        queue.extend(((x, 0), (x, height - 1)))
    for y in range(height):
        queue.extend(((0, y), (width - 1, y)))
    seeds = [pixels[x, y][:3] for x, y in queue]
    threshold = tolerance * tolerance * 3
    head = 0
    while head < len(queue):
        x, y = queue[head]
        head += 1
        index = y * width + x
        if visited[index]:
            continue
        visited[index] = 1
        r, g, b, _ = pixels[x, y]
        if not any((r - sr) ** 2 + (g - sg) ** 2 + (b - sb) ** 2 <= threshold
                   for sr, sg, sb in seeds[:8]):
            continue
        pixels[x, y] = (r, g, b, 0)
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < width and 0 <= ny < height and not visited[ny * width + nx]:
                queue.append((nx, ny))
    bbox = rgba.getbbox()
    return rgba.crop(bbox) if bbox else rgba


def template_mesh(project: GarmentProject) -> Tuple[MeshData, Color]:
    mesh = MeshData()
    w, h, d = project.width, project.height, max(project.depth, 0.01)
    name = project.template.lower()
    if "soutien" in name:
        mesh.dome(-w * 0.23, h * 0.18, d, w * 0.23, h * 0.25, d * 0.45)
        mesh.dome(w * 0.23, h * 0.18, d, w * 0.23, h * 0.25, d * 0.45)
        mesh.box((0.0, -h * 0.05, d * 0.55), (w * 0.9, h * 0.08, d * 0.25))
        mesh.box((-w * 0.27, h * 0.48, 0.0), (w * 0.06, h * 0.75, d * 0.2))
        mesh.box((w * 0.27, h * 0.48, 0.0), (w * 0.06, h * 0.75, d * 0.2))
        return mesh, (0.10, 0.14, 0.32, 1.0)
    if "culotte" in name or "calecon" in name:
        mesh.box((0.0, 0.0, d * 0.45), (w, h, d))
        mesh.box((0.0, 0.0, -d * 0.45), (w, h, d))
        return mesh, (0.10, 0.14, 0.32, 1.0)
    if "pull" in name or "t-shirt" in name:
        mesh.box((0.0, 0.0, 0.0), (w, h, d))
        mesh.box((-w * 0.62, h * 0.05, 0.0), (w * 0.25, h * 0.85, d * 0.7))
        mesh.box((w * 0.62, h * 0.05, 0.0), (w * 0.25, h * 0.85, d * 0.7))
        return mesh, (0.16, 0.28, 0.62, 1.0)
    if "pantalon" in name:
        mesh.box((-w * 0.25, -h * 0.35, 0.0), (w * 0.45, h, d))
        mesh.box((w * 0.25, -h * 0.35, 0.0), (w * 0.45, h, d))
        mesh.box((0.0, h * 0.18, 0.0), (w, h * 0.30, d))
        return mesh, (0.08, 0.12, 0.24, 1.0)
    if "chaussure" in name:
        mesh.box((0.0, 0.0, d * 0.2), (w, h * 0.45, d * 1.8))
        mesh.box((0.0, -h * 0.28, d * 0.25), (w * 1.08, h * 0.12, d * 1.9))
        return mesh, (0.05, 0.06, 0.08, 1.0)
    if "armure" in name:
        mesh.box((0.0, 0.0, d * 0.35), (w, h, d))
        mesh.box((-w * 0.62, h * 0.15, 0.0), (w * 0.24, h * 0.75, d))
        mesh.box((w * 0.62, h * 0.15, 0.0), (w * 0.24, h * 0.75, d))
        return mesh, (0.24, 0.27, 0.30, 1.0)
    mesh.box((0.0, 0.0, 0.0), (w, h, d))
    return mesh, (0.25, 0.25, 0.25, 1.0)


def image_mesh(image: "Image.Image", depth: float) -> Tuple[MeshData, Color, bytes]:
    rgba = image.convert("RGBA")
    width, height = rgba.size
    mesh = MeshData()
    mesh.box((0.0, 0.0, depth * 0.5), (1.0, height / max(width, 1), depth))
    stream = io.BytesIO()
    rgba.save(stream, format="PNG", optimize=True)
    return mesh, (1.0, 1.0, 1.0, 1.0), stream.getvalue()


class Atelier(tk.Tk):
    def __init__(self) -> None:
        super().__init__()
        self.title("LibreVies - Atelier de vetements")
        self.geometry("1120x720")
        self.minsize(900, 600)
        self.project = GarmentProject()
        self.image: Optional["Image.Image"] = None
        self.preview_image = None
        self._vars = {"name": tk.StringVar(value=self.project.name),
                      "template": tk.StringVar(value=self.project.template),
                      "mode": tk.StringVar(value=self.project.mode),
                      "anchor": tk.StringVar(value=self.project.anchor),
                      "width": tk.DoubleVar(value=self.project.width),
                      "height": tk.DoubleVar(value=self.project.height),
                      "depth": tk.DoubleVar(value=self.project.depth),
                      "scale": tk.DoubleVar(value=self.project.scale),
                      "x": tk.DoubleVar(value=0.0), "y": tk.DoubleVar(value=0.0),
                      "z": tk.DoubleVar(value=0.0), "rotation": tk.DoubleVar(value=0.0),
                      "tolerance": tk.IntVar(value=self.project.background_tolerance)}
        self._build_ui()
        self._draw_preview()

    def _build_ui(self) -> None:
        left = ttk.Frame(self, padding=10)
        left.pack(side=tk.LEFT, fill=tk.Y)
        right = ttk.Frame(self, padding=10)
        right.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True)

        ttk.Label(left, text="ATELIER DE VETEMENTS", font=("Segoe UI", 15, "bold")).pack(anchor="w", pady=(0, 12))
        ttk.Button(left, text="Importer une image", command=self._load_image).pack(fill=tk.X, pady=3)
        ttk.Button(left, text="Decouper le fond", command=self._remove_background).pack(fill=tk.X, pady=3)
        ttk.Button(left, text="Reinitialiser l'image", command=self._reset_image).pack(fill=tk.X, pady=3)
        ttk.Separator(left).pack(fill=tk.X, pady=10)

        self._field(left, "Nom", "name")
        self._combo(left, "Type", "template", ["Image decoupee", "Soutien-gorge", "Culotte", "Calecon", "Pull", "Pantalon", "Chaussure", "Armure"])
        self._combo(left, "Zone d'attache", "anchor", ["torse", "bassin", "bras gauche", "bras droit", "jambe gauche", "jambe droite", "pied", "tete"])
        self._combo(left, "Mode image", "mode", ["Image avec transparence"])
        for label, key in (("Largeur", "width"), ("Hauteur", "height"), ("Epaisseur", "depth"), ("Echelle", "scale"), ("Position X", "x"), ("Position Y", "y"), ("Position Z", "z"), ("Rotation Y", "rotation")):
            self._field(left, label, key)
        self._field(left, "Tolerance fond", "tolerance")
        ttk.Separator(left).pack(fill=tk.X, pady=10)
        ttk.Button(left, text="Sauvegarder le projet", command=self._save_project).pack(fill=tk.X, pady=3)
        ttk.Button(left, text="Ouvrir un projet", command=self._open_project).pack(fill=tk.X, pady=3)
        ttk.Button(left, text="Exporter GLB + JSON", command=self._export).pack(fill=tk.X, pady=8)
        ttk.Label(left, text="Le GLB est pret a etre importe dans Unity.", wraplength=220).pack(anchor="w", pady=5)

        self.canvas = tk.Canvas(right, background="#d0d0d0", highlightthickness=1, highlightbackground="#666666")
        self.canvas.pack(fill=tk.BOTH, expand=True)
        self.canvas.bind("<Configure>", lambda _event: self._draw_preview())

    def _field(self, parent, label: str, key: str) -> None:
        row = ttk.Frame(parent)
        row.pack(fill=tk.X, pady=2)
        ttk.Label(row, text=label, width=16).pack(side=tk.LEFT)
        entry = ttk.Entry(row, textvariable=self._vars[key], width=10)
        entry.pack(side=tk.RIGHT)
        entry.bind("<Return>", lambda _event: self._draw_preview())
        entry.bind("<FocusOut>", lambda _event: self._draw_preview())

    def _combo(self, parent, label: str, key: str, values: Sequence[str]) -> None:
        row = ttk.Frame(parent)
        row.pack(fill=tk.X, pady=2)
        ttk.Label(row, text=label, width=16).pack(side=tk.LEFT)
        box = ttk.Combobox(row, textvariable=self._vars[key], values=values, state="readonly", width=17)
        box.pack(side=tk.RIGHT)
        box.bind("<<ComboboxSelected>>", lambda _event: self._draw_preview())

    def _load_image(self) -> None:
        if Image is None:
            messagebox.showerror("Pillow manquant", "Installe Pillow avec : python -m pip install Pillow")
            return
        filename = filedialog.askopenfilename(filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.bmp"), ("Tous les fichiers", "*.*")])
        if not filename:
            return
        try:
            self.image = Image.open(filename).convert("RGBA")
            self._vars["name"].set(Path(filename).stem)
            self._vars["template"].set("Image decoupee")
            self.project.image_path = filename
            self._draw_preview()
        except Exception as exc:
            messagebox.showerror("Image impossible a ouvrir", str(exc))

    def _remove_background(self) -> None:
        if self.image is None:
            messagebox.showinfo("Image", "Importe d'abord une image.")
            return
        self.image = remove_background(self.image, int(self._vars["tolerance"].get()))
        self._draw_preview()

    def _reset_image(self) -> None:
        self.image = None
        self._draw_preview()

    def _read_project(self) -> GarmentProject:
        v = self._vars
        return GarmentProject(name=v["name"].get() or "vetement", template=v["template"].get(),
                              image_path=self.project.image_path, anchor=v["anchor"].get(),
                              width=float(v["width"].get()), height=float(v["height"].get()),
                              depth=float(v["depth"].get()), scale=float(v["scale"].get()),
                              offset_x=float(v["x"].get()), offset_y=float(v["y"].get()),
                              offset_z=float(v["z"].get()), rotation_y=float(v["rotation"].get()),
                              background_tolerance=int(v["tolerance"].get()), mode=v["mode"].get())

    def _draw_preview(self) -> None:
        if not hasattr(self, "canvas"):
            return
        self.canvas.delete("all")
        width = max(self.canvas.winfo_width(), 400)
        height = max(self.canvas.winfo_height(), 300)
        self.canvas.create_text(18, 18, anchor="nw", text="Apercu du vetement", fill="#222", font=("Segoe UI", 13, "bold"))
        if self.image is not None and ImageTk is not None:
            image = self.image.copy()
            image.thumbnail((width - 80, height - 100), Image.Resampling.LANCZOS)
            self.preview_image = ImageTk.PhotoImage(image)
            self.canvas.create_image(width / 2, height / 2, image=self.preview_image)
        else:
            project = self._read_project()
            cx, cy = width / 2, height / 2
            sx = max(30, min(width * 0.35, project.width * 450))
            sy = max(30, min(height * 0.35, project.height * 450))
            if "Soutien" in project.template:
                self.canvas.create_oval(cx - sx * .48, cy - sy * .1, cx - sx * .02, cy + sy * .45, fill="#283f8f", outline="#17244f", width=2)
                self.canvas.create_oval(cx + sx * .02, cy - sy * .1, cx + sx * .48, cy + sy * .45, fill="#283f8f", outline="#17244f", width=2)
                self.canvas.create_line(cx - sx * .3, cy - sy * .05, cx - sx * .4, cy - sy * .7, fill="#17244f", width=5)
                self.canvas.create_line(cx + sx * .3, cy - sy * .05, cx + sx * .4, cy - sy * .7, fill="#17244f", width=5)
            else:
                self.canvas.create_rectangle(cx - sx / 2, cy - sy / 2, cx + sx / 2, cy + sy / 2, fill="#283f8f", outline="#17244f", width=2)
            self.canvas.create_text(cx, height - 28, text="Apercu parametrique - exporte ensuite en GLB", fill="#333")

    def _save_project(self) -> None:
        project = self._read_project()
        filename = filedialog.asksaveasfilename(defaultextension=".json", filetypes=[("Projet vetement", "*.json")], initialfile=project.name + ".json")
        if not filename:
            return
        Path(filename).write_text(json.dumps(asdict(project), indent=2, ensure_ascii=False), encoding="utf-8")
        messagebox.showinfo("Projet", "Projet sauvegarde.")

    def _open_project(self) -> None:
        filename = filedialog.askopenfilename(filetypes=[("Projet vetement", "*.json")])
        if not filename:
            return
        try:
            data = json.loads(Path(filename).read_text(encoding="utf-8"))
            self.project = GarmentProject(**{k: v for k, v in data.items() if k in GarmentProject.__dataclass_fields__})
            for key, value in (("name", self.project.name), ("template", self.project.template), ("mode", self.project.mode), ("anchor", self.project.anchor), ("width", self.project.width), ("height", self.project.height), ("depth", self.project.depth), ("scale", self.project.scale), ("x", self.project.offset_x), ("y", self.project.offset_y), ("z", self.project.offset_z), ("rotation", self.project.rotation_y), ("tolerance", self.project.background_tolerance)):
                self._vars[key].set(value)
            if self.project.image_path and Image is not None and Path(self.project.image_path).exists():
                self.image = Image.open(self.project.image_path).convert("RGBA")
            self._draw_preview()
        except Exception as exc:
            messagebox.showerror("Projet impossible a ouvrir", str(exc))

    def _export(self) -> None:
        if Image is None:
            messagebox.showerror("Pillow manquant", "Installe Pillow avec : python -m pip install Pillow")
            return
        project = self._read_project()
        folder = filedialog.askdirectory(title="Dossier d'export du vetement")
        if not folder:
            return
        output = Path(folder)
        texture = None
        if self.image is not None and project.template == "Image decoupee":
            mesh, color, texture = image_mesh(self.image, project.depth)
        else:
            mesh, color = template_mesh(project)
        glb_path = output / (project.name + ".glb")
        GLBWriter.write(glb_path, mesh, color, texture,
                        (project.offset_x, project.offset_y, project.offset_z),
                        project.scale, project.rotation_y)
        metadata = asdict(project)
        metadata["export"] = glb_path.name
        metadata["format"] = "GLB"
        metadata["version"] = 1
        (output / (project.name + ".json")).write_text(json.dumps(metadata, indent=2, ensure_ascii=False), encoding="utf-8")
        messagebox.showinfo("Export termine", f"Fichiers crees :\n{glb_path.name}\n{project.name}.json")


def main() -> None:
    if Image is None:
        print("Pillow est necessaire : python -m pip install Pillow", file=sys.stderr)
        raise SystemExit(2)
    Atelier().mainloop()


if __name__ == "__main__":
    main()
