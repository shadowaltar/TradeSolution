package com.sandbox.controllers;

import com.sandbox.services.FileService;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.io.File;
import java.util.Arrays;
import java.util.List;

@RestController()
@RequestMapping(value = "/files")
public class FileController {

    @Autowired
    private FileService service;

    @GetMapping("root-all")
    public ResponseEntity<List<File>> getRootAll(
            @RequestParam(name = "disk-letter") String diskLetter) {
        if (diskLetter.isBlank())
            return ResponseEntity.badRequest().build();
        File[] files = service.getRootFolderFiles(diskLetter);
        return ResponseEntity.ok(Arrays.stream(files).toList());
    }
}
