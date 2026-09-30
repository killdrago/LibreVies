#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Atelier 3D leger de LibreVies, sans Blender.

Cette interface reutilise l'exporteur GLB de vetement.py et ajoute un apercu
3D interactif au-dessus d'une geometrie parametrique. Une photo sert de
texture et de silhouette decoupee, mais ne peut pas deviner seule le dos, la
profondeur et les coutures d'un vrai vetement : le type et les dimensions
restent reglables dans l'atelier.
"""

from __future__ import annotations

import json
import math
import tkinter as tk
from pathlib import Path
from tkinter import messagebox
from typing import List, Optional, Tuple

from vetement import (Atelier, Color, GLBWriter, Image, MeshData,
                      ImageTk, asdict, image_mesh, template_mesh)


class Atelier3D(Atelier):
    """Version avec viewport 3D logiciel, sans OpenGL ni Blender."""

    def __init__(self) -> None:
        self.view_yaw = -0.18
        self.view_pitch = 0.06
        self.view_zoom = 1.0
        self._drag = None
        super().__init__()
        self.canvas.bind("<ButtonPress-1>", self._start_orbit)
        self.canvas.bind("<B1-Motion>", self._orbit)
        self.canvas.bind("<ButtonRelease-1>", self._stop_orbit)
        self.canvas.bind("<MouseWheel>", self._wheel)
        self.canvas.bind("<Button-4>", lambda event: self._zoom(1.10))
        self.canvas.bind("<Button-5>", lambda event: self._zoom(0.90))

    def _build_ui(self) -> None:
        super()._build_ui()
        ttk_label = tk.Label(
            self.canvas,
            text="Vue 3D : glisser pour tourner • molette pour zoomer",
            bg="#d0d0d0",
            fg="#333333",
            font=("Segoe UI", 9),
        )
        ttk_label.place(relx=0.02, rely=0.96, anchor="sw")
        self._viewport_hint = ttk_label

    def _start_orbit(self, event) -> None:
        self._drag = (event.x, event.y, self.view_yaw, self.view_pitch)

    def _orbit(self, event) -> None:
        if self._drag is None:
            return
        x0, y0, yaw, pitch = self._drag
        self.view_yaw = yaw + (event.x - x0) * 0.012
        self.view_pitch = max(-1.25, min(1.25, pitch + (event.y - y0) * 0.012))
        self._draw_preview()

    def _stop_orbit(self, _event) -> None:
        self._drag = None

    def _wheel(self, event) -> None:
        self._zoom(1.10 if event.delta > 0 else 0.90)

    def _zoom(self, factor: float) -> None:
        self.view_zoom = max(0.35, min(4.0, self.view_zoom * factor))
        self._draw_preview()

    @staticmethod
    def _rotate(point: Tuple[float, float, float], yaw: float,
                pitch: float) -> Tuple[float, float, float]:
        x, y, z = point
        cy, sy = math.cos(yaw), math.sin(yaw)
        x1 = cy * x + sy * z
        z1 = -sy * x + cy * z
        cp, sp = math.cos(pitch), math.sin(pitch)
        y2 = cp * y - sp * z1
        z2 = sp * y + cp * z1
        return x1, y2, z2

    @staticmethod
    def _face_normal(a: Tuple[float, float, float],
                    b: Tuple[float, float, float],
                    c: Tuple[float, float, float]) -> Tuple[float, float, float]:
        ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
        vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
        nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
        length = math.sqrt(nx * nx + ny * ny + nz * nz) or 1.0
        return nx / length, ny / length, nz / length

    @staticmethod
    def _shade(color: Color, normal: Tuple[float, float, float]) -> str:
        # Une lumiere douce venant de l'avant et du haut.
        lx, ly, lz = 0.30, 0.72, 0.64
        light_length = math.sqrt(lx * lx + ly * ly + lz * lz)
        dot = max(0.0, (normal[0] * lx + normal[1] * ly + normal[2] * lz) / light_length)
        amount = 0.48 + dot * 0.52
        r = max(0, min(255, int(color[0] * 255 * amount)))
        g = max(0, min(255, int(color[1] * 255 * amount)))
        b = max(0, min(255, int(color[2] * 255 * amount)))
        return f"#{r:02x}{g:02x}{b:02x}"

    def _draw_preview(self) -> None:
        if not hasattr(self, "canvas"):
            return
        self.canvas.delete("all")
        width = max(self.canvas.winfo_width(), 400)
        height = max(self.canvas.winfo_height(), 300)
        self.canvas.create_text(
            18, 18, anchor="nw", text="Apercu 3D du vetement",
            fill="#222", font=("Segoe UI", 13, "bold"),
        )
        try:
            project = self._read_project()
            if self.image is not None and project.template == "Image decoupee":
                mesh, color, _texture = image_mesh(self.image, project.depth)
            else:
                mesh, color = template_mesh(project)
        except Exception as exc:
            self.canvas.create_text(width / 2, height / 2, text=str(exc), fill="#a00")
            return

        points = [self._rotate(p, self.view_yaw, self.view_pitch) for p in mesh.positions]
        extent = max(
            [max(abs(p[0]), abs(p[1]), abs(p[2])) for p in points] or [1.0]
        )
        scale = min(width * 0.36, height * 0.36) / max(extent, 0.001)
        scale *= self.view_zoom
        cx, cy = width * 0.50, height * 0.53

        triangles: List[Tuple[float, List[Tuple[float, float]], str]] = []
        for index in range(0, len(mesh.indices) - 2, 3):
            ia, ib, ic = mesh.indices[index:index + 3]
            if max(ia, ib, ic) >= len(points):
                continue
            a, b, c = points[ia], points[ib], points[ic]
            normal = self._face_normal(a, b, c)
            screen = [(cx + p[0] * scale, cy - p[1] * scale)
                      for p in (a, b, c)]
            depth = (a[2] + b[2] + c[2]) / 3.0
            triangles.append((depth, screen, self._shade(color, normal)))

        # Peindre les faces lointaines d'abord pour obtenir un vrai apercu 3D.
        for _depth, polygon, fill in sorted(triangles, key=lambda item: item[0]):
            self.canvas.create_polygon(
                polygon, fill=fill, outline="#1b2443", width=1,
            )
        self.canvas.create_text(
            cx, height - 32,
            text="Mesh parametrique 3D • photo utilisee comme texture a l'export",
            fill="#333",
        )

    def _export(self) -> None:
        """Exporte aussi la texture PNG pour que Unity puisse la reutiliser."""
        if Image is None:
            messagebox.showerror(
                "Pillow manquant",
                "Installe Pillow avec : python -m pip install Pillow",
            )
            return
        project = self._read_project()
        folder = self._ask_export_folder()
        if folder is None:
            return
        output = Path(folder)

        texture = None
        if self.image is not None and project.template == "Image decoupee":
            mesh, color, texture = image_mesh(self.image, project.depth)
        else:
            mesh, color = template_mesh(project)
            if self.image is not None:
                stream = __import__("io").BytesIO()
                self.image.convert("RGBA").save(stream, format="PNG", optimize=True)
                texture = stream.getvalue()

        glb_path = output / (project.name + ".glb")
        GLBWriter.write(
            glb_path, mesh, color, texture,
            (project.offset_x, project.offset_y, project.offset_z),
            project.scale, project.rotation_y,
        )
        metadata = asdict(project)
        metadata.update({
            "export": glb_path.name,
            "format": "GLB",
            "version": 2,
            "preview": "atelier3d",
            "skinning": "Unity transfers body bone weights at runtime",
            "photo_to_3d": "parametric mesh with photo texture; depth is edited manually",
        })
        if texture is not None:
            texture_path = output / (project.name + ".png")
            texture_path.write_bytes(texture)
            metadata["texture"] = texture_path.name
        (output / (project.name + ".json")).write_text(
            json.dumps(metadata, indent=2, ensure_ascii=False), encoding="utf-8",
        )
        messagebox.showinfo(
            "Export termine",
            "Fichiers crees :\n" + glb_path.name + "\n" + project.name + ".json"
            + ("\n" + project.name + ".png" if texture is not None else ""),
        )

    def _ask_export_folder(self) -> Optional[str]:
        # La methode de l'atelier existant est volontairement appelee ici pour
        # garder le meme comportement de dialogue Windows.
        from tkinter import filedialog
        return filedialog.askdirectory(title="Dossier d'export du vetement")


if __name__ == "__main__":
    if Image is None:
        raise SystemExit("Pillow est necessaire : python -m pip install Pillow")
    Atelier3D().mainloop()
