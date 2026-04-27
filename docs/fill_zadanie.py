#!/usr/bin/env python3
"""Подмена данных в Листе задания, который пришёл от руководителя.

Принимает на вход реальный Лист задания (с примером темы / ФИО) и подменяет:
  - тему работы на нашу («Тихий час»);
  - ФИО студента на плейсхолдер _____________;
  - список разделов ПЗ на наш фактический;
  - список приложений (А, Б, В, Г, Д) на наши.

Не трогает:
  - реквизиты колледжа, ФИО зам. директора и председателя ЦМК;
  - дату выдачи и срок окончания;
  - ФИО руководителя (его поставит сам руководитель).
"""
from __future__ import annotations

import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO / "docs"))

import docx  # noqa: E402
from docx.oxml.ns import qn  # noqa: E402

from build_vkr import PLACEHOLDER, replace_in_doc  # noqa: E402


THEME_PARTS = {
    "Разработка программных модулей для организации":
        "Разработка модулей системы управления",
    "проведения мероприятий в агентстве деловых коммуникаций":
        "взаимоотношениями с клиентами сети барбершопов",
    "«Премьер Консалт»":
        "«Тихий час»",
}

STUDENT = {
    "Петруновскому Никите Дмитриевичу": PLACEHOLDER,
}


# Содержимое каждой строки списка разделов ПЗ — по фактическому индексу
# строки внешней таблицы. См. дамп таблицы: rows 20–39 содержат заголовок
# «ПОЯСНИТЕЛЬНАЯ ЗАПИСКА» и подразделы. Здесь — только ячейки с разделами.
CHAPTER_ROWS = {
    20: "1 Назначение и цели разработки программных модулей",
    21: "2 Анализ предметной области и постановка задачи",
    22: "2.1 Описание предметной области сети барбершопов",
    23: "2.2 Описание постановки задачи",
    24: "2.3 Схема функциональной структуры",
    25: "2.4 Модель данных (паттерн Persona)",
    26: "2.5 Бизнес-правила и сценарии записи",
    27: "3 Реализация",
    28: "3.1 Обоснование выбора средств разработки",
    29: "3.2 Сервисный слой и расчёт свободных слотов",
    30: "3.3 Программная реализация модулей",
    31: "4 Тестирование",
    32: "4.1 Стратегия тестирования",
    33: "4.2 Модульное тестирование сервисного слоя",
    34: "5 Эксплуатационная документация",
    35: "5.1 Руководство пользователя онлайн-записи",
    36: "5.2 Руководство владельца сети",
    37: "5.3 Условия эксплуатации",
    38: "Заключение",
    39: "Список использованных источников",
}


# Названия приложений по индексу строки во вложенной таблице приложений.
APPENDIX_ROWS = {
    1: "Техническое задание (требования к системе)",  # А
    2: "Программный код модулей системы",  # Б
    3: "Структура базы данных",  # В
    4: "Экранные формы пользовательского интерфейса",  # Г
    5: "Тест-кейсы",  # Д
}


def _strip_numbering(paragraph) -> None:
    """Убрать автонумерацию (w:numPr) у параграфа.

    Нужно для строк, в которых исходный шаблон использовал нумерованный
    список (auto-номера 2.2, 2.3, 2.4 и т. п.), а мы хотим, чтобы номер
    был явным в тексте."""
    pPr = paragraph._element.find(qn("w:pPr"))
    if pPr is None:
        return
    numPr = pPr.find(qn("w:numPr"))
    if numPr is not None:
        pPr.remove(numPr)


def _set_cell_text_inplace(cell, new_text: str) -> None:
    """Заменить текст ячейки, сохранив форматирование первого run-а первого параграфа.
    Удаляет лишние параграфы и убирает автонумерацию."""
    if not cell.paragraphs:
        cell.add_paragraph(new_text)
        return
    p = cell.paragraphs[0]
    _strip_numbering(p)
    if not p.runs:
        p.add_run(new_text)
    else:
        p.runs[0].text = new_text
        for r in p.runs[1:]:
            r.text = ""
    # Удалить все остальные параграфы ячейки.
    for extra in cell.paragraphs[1:]:
        extra._element.getparent().remove(extra._element)


def fill_chapters(doc: docx.document.Document) -> None:
    outer = doc.tables[0]
    for row_idx, text in CHAPTER_ROWS.items():
        cell = outer.rows[row_idx].cells[0]
        _set_cell_text_inplace(cell, text)


def fill_appendices(doc: docx.document.Document) -> None:
    outer = doc.tables[0]
    nested = outer.rows[40].cells[0].tables[0]
    for row_idx, title in APPENDIX_ROWS.items():
        cell = nested.rows[row_idx].cells[1]
        _set_cell_text_inplace(cell, title)


def main() -> None:
    in_path = Path(sys.argv[1]) if len(sys.argv) > 1 else REPO / "build" / "_zadanie_in.docx"
    out_path = Path(sys.argv[2]) if len(sys.argv) > 2 else REPO / "build" / "02_zadanie_filled.docx"
    out_path.parent.mkdir(parents=True, exist_ok=True)

    doc = docx.Document(str(in_path))

    # Тема и ФИО студента — текстовые замены по подстроке.
    replace_in_doc(doc, THEME_PARTS)
    replace_in_doc(doc, STUDENT)
    # Список разделов ПЗ и приложений — точечная замена ячеек.
    fill_chapters(doc)
    fill_appendices(doc)

    doc.save(str(out_path))
    print(f"OK: {out_path}")


if __name__ == "__main__":
    main()
